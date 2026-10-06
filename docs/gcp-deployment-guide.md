# PARCS — Google Cloud Deployment Guide (CLI, manual, no Terraform)

This supersedes `parcs_gcp_guide.docx` (which only exists on the old `feature/GCloud`
branch and assumed the Cloud Console + a US region + the trial-credit project
`parcs-gcp`). This version documents the actual commands used to deploy PARCS to
GKE by hand via `gcloud` / `kubectl` / `helm`, with no Terraform/Bicep and no
Cloud Console clicking — now including GPU support (`feature/GPU`).

## Current state (as of writing)

**Everything described here has been torn down.** The GKE cluster, both node
pools, all Pub/Sub topics/subscriptions, and the orphaned PVC-backed disks were
all deleted after the GPU benchmark work paused on a real-world T4 stockout (see
§10). Nothing in this project is running or billing right now except:

- The GCP project itself (`project-42bf3d0d-188a-4d3d-b56`, region `europe-central2`,
  billing linked to account `01C901-AEF94D-299296`, now upgraded to paid — see §9).
- IAM service accounts `parcs-host` / `parcs-daemon` / `keda-operator` and their
  Workload Identity bindings — zero cost, kept so redeployment doesn't need to
  recreate IAM.
- Artifact Registry repo `parcs` (europe-central2) with the images already built —
  ~$0.15/month, kept so redeployment doesn't need to rebuild/push (~5-10 min saved).

Redeploying from here means: §2 (cluster) → §3 (Pub/Sub) → §5-7 (KEDA/storage/apply)
→ §10 (GPU node pool, if needed). §4 (service accounts/IAM) and image builds can be
skipped entirely — both already exist and are listed below.

**Existing Artifact Registry images** (don't rebuild unless source changed):
```
europe-central2-docker.pkg.dev/project-42bf3d0d-188a-4d3d-b56/parcs/parcshost:latest
europe-central2-docker.pkg.dev/project-42bf3d0d-188a-4d3d-b56/parcs/parcsportal:latest
europe-central2-docker.pkg.dev/project-42bf3d0d-188a-4d3d-b56/parcs/parcsdaemon:latest       # CPU, plain .NET runtime base
europe-central2-docker.pkg.dev/project-42bf3d0d-188a-4d3d-b56/parcs/parcsdaemon-gpu:latest   # GPU, CUDA 12.3 base — see §10
europe-central2-docker.pkg.dev/project-42bf3d0d-188a-4d3d-b56/parcs/parcs-agent-mcp:latest
```

## 0. Prerequisites

- `gcloud`, `kubectl`, `helm` installed and `gcloud auth login` done.
- A GCP project with **billing enabled AND not on the free-tier trial restriction**.
  `billingEnabled: true` is not sufficient — see §9, a free-tier/trial billing
  account hard-blocks GPU VM creation specifically (CPU is unaffected).
  Verify: `gcloud billing projects describe <PROJECT_ID>` and
  `gcloud billing accounts describe <ACCOUNT_ID>` (`open: true` required).
- Pick a region close to your users. Blazor Server (the Portal) round-trips every
  UI interaction over SignalR, so region choice directly affects how the UI feels,
  not just initial load time. For Ukraine, `europe-central2` (Warsaw) is GCP's
  closest region.
- If you plan to use GPUs: GPU quota (`NVIDIA_T4_GPUS` etc.) is a **flat per-project
  default of 1, identical in every region** (verified across 6 EU + 5 US regions) —
  region choice does not affect quota. It only affects real-time stock availability,
  which is independent per zone and *does* vary — see §10.

## 1. Enable APIs

```bash
PROJECT=<your-project-id>
gcloud config set project $PROJECT
gcloud services enable \
  compute.googleapis.com \
  container.googleapis.com \
  pubsub.googleapis.com \
  iam.googleapis.com \
  iamcredentials.googleapis.com \
  cloudresourcemanager.googleapis.com \
  artifactregistry.googleapis.com \
  cloudbuild.googleapis.com \
  --project=$PROJECT
```

Filestore API is **not needed** — see the storage note in §6.

## 2. Create the GKE cluster

```bash
gcloud container clusters create parcs-cluster \
  --project=$PROJECT \
  --zone=europe-central2-a \
  --release-channel=regular \
  --workload-pool=${PROJECT}.svc.id.goog \
  --num-nodes=3 \
  --machine-type=n1-standard-2 \
  --disk-type=pd-standard \
  --disk-size=50 \
  --enable-autoscaling --min-nodes=3 --max-nodes=9 \
  --enable-ip-alias
```

This one command replaces the VPC/subnet/cluster/node-pool Terraform resources —
GKE auto-creates the VPC-native subnet with pod/service secondary ranges when you
pass `--enable-ip-alias` without specifying an existing subnet. Takes 5-10 min.
`gcloud` automatically writes the kubeconfig entry and switches your current
context to the new cluster.

## 3. Pub/Sub

```bash
gcloud pubsub topics create point-requested --project=$PROJECT
gcloud pubsub topics create point-requested-dlq --project=$PROJECT
gcloud pubsub subscriptions create point-requested-dlq-sub \
  --topic=point-requested-dlq --ack-deadline=600 --project=$PROJECT
gcloud pubsub subscriptions create point-requested-sub \
  --topic=point-requested \
  --ack-deadline=60 \
  --message-retention-duration=600s \
  --dead-letter-topic=point-requested-dlq \
  --max-delivery-attempts=5 \
  --min-retry-delay=10s --max-retry-delay=60s \
  --project=$PROJECT
```

**Gotcha:** dead-letter delivery silently doesn't work unless the Pub/Sub service
agent itself has publish rights on the DLQ topic and subscribe rights on the
source subscription. Grant these explicitly:

```bash
PROJECT_NUMBER=$(gcloud projects describe $PROJECT --format="value(projectNumber)")
PUBSUB_SA="service-${PROJECT_NUMBER}@gcp-sa-pubsub.iam.gserviceaccount.com"
gcloud pubsub topics add-iam-policy-binding point-requested-dlq \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.publisher" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding point-requested-sub \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.subscriber" --project=$PROJECT
```

**Gotcha — KEDA's trigger metric lags real time by minutes.** KEDA's `gcp-pubsub`
trigger scales purely off the Cloud Monitoring `num_undelivered_messages` metric,
not the live subscription state. Measured directly: a message published at T+0
didn't show up in the metric until **T+4 minutes**, so KEDA had nothing to scale
on until then. On top of that, a cold daemon pod + (for GPU) a cold node can add
several more minutes. `HostTcpConfiguration.DaemonConnectTimeoutSeconds` (in
`src/Parcs.Core/Configuration/HostTcpConfiguration.cs`) is set to **420s** to
clear this — don't lower it without re-measuring the metric lag on your project
first; a quiet/low-traffic subscription (like a dev project) sees the worst lag.

## 4. Service accounts + IAM + Workload Identity

Skip this whole section if `parcs-host` / `parcs-daemon` / `keda-operator` service
accounts already exist in the project (check `gcloud iam service-accounts list`) —
these and their Workload Identity bindings are project-global and don't need to be
recreated just because the cluster/Pub/Sub resources were torn down and rebuilt.

```bash
gcloud iam service-accounts create parcs-host --display-name="PARCS Host Service Account" --project=$PROJECT
gcloud iam service-accounts create parcs-daemon --display-name="PARCS Daemon Service Account" --project=$PROJECT
gcloud iam service-accounts create keda-operator --display-name="KEDA Operator Service Account" --project=$PROJECT

gcloud pubsub topics add-iam-policy-binding point-requested \
  --member="serviceAccount:parcs-host@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.publisher" --project=$PROJECT
gcloud pubsub topics add-iam-policy-binding point-requested \
  --member="serviceAccount:parcs-daemon@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.viewer" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding point-requested-sub \
  --member="serviceAccount:parcs-daemon@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.subscriber" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding point-requested-sub \
  --member="serviceAccount:keda-operator@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.viewer" --project=$PROJECT
gcloud projects add-iam-policy-binding $PROJECT \
  --member="serviceAccount:keda-operator@${PROJECT}.iam.gserviceaccount.com" --role="roles/monitoring.viewer" --condition=None

gcloud iam service-accounts add-iam-policy-binding parcs-host@${PROJECT}.iam.gserviceaccount.com \
  --role="roles/iam.workloadIdentityUser" --member="serviceAccount:${PROJECT}.svc.id.goog[default/parcs-host]" --project=$PROJECT
gcloud iam service-accounts add-iam-policy-binding parcs-daemon@${PROJECT}.iam.gserviceaccount.com \
  --role="roles/iam.workloadIdentityUser" --member="serviceAccount:${PROJECT}.svc.id.goog[default/parcs-daemon]" --project=$PROJECT
gcloud iam service-accounts add-iam-policy-binding keda-operator@${PROJECT}.iam.gserviceaccount.com \
  --role="roles/iam.workloadIdentityUser" --member="serviceAccount:${PROJECT}.svc.id.goog[keda/keda-operator]" --project=$PROJECT
```

If redeploying the **GPU** path too, these same three service accounts also need
bindings on the `gpu-point-requested*` topic/subscription — see §10.

**Important:** `kube/deployment.gcp.yaml` hardcodes the GCP service-account emails
in the two `ServiceAccount` annotations (`parcs-daemon@...` / `parcs-host@...`)
rather than using a `PROJECT_ID` placeholder token — they're currently set to
`project-42bf3d0d-188a-4d3d-b56`. If you deploy to a different project, edit those
two `iam.gke.io/gcp-service-account` annotation values first.

## 5. Install KEDA

```bash
kubectl apply --server-side --force-conflicts \
  -f https://github.com/kedacore/keda/releases/download/v2.12.0/keda-2.12.0.yaml
```

**Gotcha:** plain `kubectl apply -f` on this file fails with
`metadata.annotations: Too long: may not be more than 262144 bytes` — the KEDA
v2.12 CRDs (particularly `scaledjobs.keda.sh`) are too large to fit in the
`kubectl.kubernetes.io/last-applied-configuration` annotation that client-side
`apply` writes. Use `--server-side --force-conflicts` instead; there's no
annotation-size limit on the server-side apply path.

```bash
kubectl wait --for=condition=Ready pod -l app=keda-operator -n keda --timeout=120s
kubectl annotate serviceaccount keda-operator -n keda \
  iam.gke.io/gcp-service-account=keda-operator@${PROJECT}.iam.gserviceaccount.com
```

## 6. RWX storage — skip Filestore

Filestore's 1 TB minimum (~$200/month) is disproportionate for anything but a
production-scale deployment. Use the NFS-Ganesha chart instead:

```bash
helm repo add nfs-ganesha-server-and-external-provisioner \
  https://kubernetes-sigs.github.io/nfs-ganesha-server-and-external-provisioner/
helm repo update

helm install nfs-server nfs-ganesha-server-and-external-provisioner/nfs-server-provisioner \
  --set persistence.enabled=true \
  --set persistence.size=2Gi \
  --set storageClass.name=parcs-rwx \
  --set storageClass.defaultClass=false

kubectl get storageclass parcs-rwx   # confirm it exists before applying the manifest
```

Note this storage class isn't defined anywhere in `deployment.gcp.yaml` itself —
without this Helm install, `parcs-storage-volume-claim` (and anything mounting
it — Host, the daemon jobs, the MCP server) stays `Pending` forever.

**Gotcha — `helm uninstall` leaks a disk.** The chart's own StatefulSet PVC
(`data-nfs-server-nfs-server-provisioner-0`) is deliberately *not* deleted by
`helm uninstall` (standard Helm behavior — StatefulSet PVCs are left alone to
prevent accidental data loss). If you tear the cluster down, that PVC's backing
GCE disk survives as an orphan and keeps billing (~$0.08/GB/month) until you
delete it manually: `gcloud compute disks list` → find the `pvc-*` disk with no
cluster referencing it → `gcloud compute disks delete <name> --zone=<zone>`.

To move to Filestore later for production: switch the `parcs-rwx` StorageClass to
the `filestore.csi.storage.gke.io` provisioner and delete this Helm release. No
PVC definitions need to change.

## 7. Deploy PARCS

```bash
kubectl create secret generic parcs-gcp-secret --from-literal=projectId="${PROJECT}"
kubectl apply -f kube/deployment.gcp.yaml
kubectl get pods -w
```

## 8. Verify

```bash
kubectl get pvc                          # all should be Bound
kubectl get pods                         # all Running (agent-mcp, hostapi, portal, database, elasticsearch, kibana, nfs-server)
kubectl get svc parcs-portal parcs-hostapi parcs-agent-mcp-external   # wait for EXTERNAL-IP
kubectl logs -l app=parcs-agent-mcp --tail=50   # confirm "AgentRunner module registered — id=<N>"
```

Open `http://<parcs-portal external IP>` for the Portal. The MCP server has no
auth (removed — see git history on `src/Parcs.Agent.Mcp/Program.cs` if it needs
to come back):

```bash
claude mcp add parcs --transport http http://<parcs-agent-mcp-external external IP>:8080/
```

## 9. The free-tier/trial billing GPU block — and how we got past it

**Symptom:** GPU node creation fails with every attempt, even though
`gcloud compute regions describe` shows `NVIDIA_T4_GPUS` quota = 1 (not 0). The
actual error, found via `gcloud compute instance-groups managed list-errors`:

> Your billing account is currently in the free tier where non-TPU accelerators
> are not available. Please upgrade to a paid billing account...

This is a **hard provisioning-level gate independent of the quota number shown**.
`billingEnabled: true` on the project does not mean the billing account itself is
out of free-tier/trial status.

- Self-service quota increases via `gcloud alpha services quota update` are also
  capped at a max of 1 while on this restriction
  (`COMMON_QUOTA_CONSUMER_OVERRIDE_TOO_HIGH`) — upgrading billing is the only way
  past both problems.
- **There is no `gcloud`/API command to upgrade a billing account to paid** — it's
  a Console-only action (`console.cloud.google.com` → "Upgrade my account" banner,
  requires confirming a real payment method).
- If upgrading the project's own billing account isn't possible (e.g. no API
  access to it — `gcloud billing accounts describe <id>` returns a permission
  error even though `gcloud billing accounts list` shows it), check whether a
  *different* billing account you do control is already paid/open
  (`gcloud billing accounts describe <id>` → `open: true`), and re-point the
  project at that one instead of migrating any infrastructure:
  ```bash
  gcloud billing projects link $PROJECT --billing-account=<OPEN_PAID_ACCOUNT_ID>
  ```
  This is a one-line fix and doesn't require rebuilding anything.

## 10. GPU support (`feature/GPU`)

### Architecture

A **fully separate path from the CPU daemon**, deliberately not sharing the
`parcs-daemon-scaler` ScaledJob — with only 1 GPU available (and per-project quota
flat at 1 everywhere regardless of region, see §0), pointing the *existing* shared
pool at the GPU node would cap *all* jobs, GPU or not, at 1 concurrent daemon.
Instead:

- A second Pub/Sub topic/subscription pair (`gpu-point-requested*`), mirroring the
  CPU ones exactly.
- A second KEDA ScaledJob, `parcs-daemon-gpu-scaler`, using the **GPU daemon image**
  (`parcsdaemon-gpu`, built from `src/Parcs.Daemon/Dockerfile.gpu` — CUDA 12.3 base
  with the .NET 10 runtime copied in from the official runtime image rather than
  apt-installed, since Microsoft's apt feed for jammy doesn't have
  `dotnet-runtime-10.0` packages), with `nodeSelector: {accelerator: nvidia}`,
  a toleration for the `sku=gpu:NoSchedule` taint, and `nvidia.com/gpu: 1` in both
  `resources.limits` and `resources.requests`.
- A fixed-size (not pod-autoscaled) **GPU node pool**, `gpu-pool`, sized to current
  quota (1 node), with the GKE *cluster* autoscaler (not KEDA) handling 0↔1 node
  scaling — this still satisfies "no KEDA/pod-count autoscaling for GPU daemons"
  while letting the node itself disappear (and stop billing) when idle.
- **Routing:** `JobEntity.RequiresGpu` (bool column, migration
  `20261001172812_AddRequiresGpuToJobs`) is set explicitly at job-creation time
  (Portal: a "Run on GPU" checkbox on the New Job form; API: a `RequiresGpu` form
  field alongside `AssemblyName`/`ClassName`/`ModuleId` on `POST /api/Jobs`).
  `PointCreationService` looks this flag up by `jobId` and picks
  `PubSubConfiguration.TopicId` vs `.GpuTopicId` accordingly — no changes needed to
  `Parcs.Net`, `IModuleInfo`, or the module-author API; this is the *only* place
  GPU-vs-CPU routing happens.
- GPU execution itself needs **zero new dispatch logic** beyond that routing: a
  GPU-capable module just ships a `Gpu/` sub-namespace alongside its CPU `Parallel/`
  variant in the same assembly, and the caller picks the `Gpu.*MainModule` class
  name instead of the `Parallel.*MainModule` one. The Portal's existing "Class"
  dropdown already lists every `IModule` implementation found via reflection in an
  uploaded assembly — GPU classes show up automatically, no Portal change needed
  for *that* part.

### Create the GPU node pool

```bash
gcloud container node-pools create gpu-pool \
  --cluster=parcs-cluster \
  --zone=europe-central2-a \
  --project=$PROJECT \
  --machine-type=n1-standard-4 \
  --accelerator=type=nvidia-tesla-t4,count=1,gpu-driver-version=default \
  --num-nodes=0 \
  --enable-autoscaling --min-nodes=0 --max-nodes=1 \
  --node-taints=sku=gpu:NoSchedule \
  --node-labels=accelerator=nvidia \
  --disk-type=pd-standard --disk-size=50
```

**GPU type: T4, not K80.** The old spec (written for Azure/AKS) used K80 because
that's what had non-zero quota in Azure's `eastus`. On GCP, K80 **isn't even an
available accelerator type** in `europe-central2` zones (checked via
`gcloud compute accelerator-types list`) — T4 is, and it's strictly better
hardware (newer compute capability, no driver-deprecation risk). Don't carry the
K80 choice over from the old spec when porting to GCP.

**GPU node pools need a zone with actual T4 capacity, which is not the same thing
as the cluster's own zone.** Our cluster control plane is `europe-central2-a`, but
`nvidia-tesla-t4` is only offered in `europe-central2-b`/`-c`. Use
`--node-locations` to place just this node pool in a different zone than the
cluster's primary one (`--zone` still identifies which cluster/location to attach
to, `--node-locations` controls where the pool's actual nodes land):

```bash
# only needed if your cluster's own zone doesn't have the accelerator type
--node-locations=europe-central2-b
```

### Pub/Sub + IAM for the GPU path (additive to §3/§4)

```bash
gcloud pubsub topics create gpu-point-requested --project=$PROJECT
gcloud pubsub topics create gpu-point-requested-dlq --project=$PROJECT
gcloud pubsub subscriptions create gpu-point-requested-dlq-sub \
  --topic=gpu-point-requested-dlq --ack-deadline=600 --project=$PROJECT
gcloud pubsub subscriptions create gpu-point-requested-sub \
  --topic=gpu-point-requested --ack-deadline=60 --message-retention-duration=600s \
  --dead-letter-topic=gpu-point-requested-dlq --max-delivery-attempts=5 \
  --min-retry-delay=10s --max-retry-delay=60s --project=$PROJECT

PROJECT_NUMBER=$(gcloud projects describe $PROJECT --format="value(projectNumber)")
PUBSUB_SA="service-${PROJECT_NUMBER}@gcp-sa-pubsub.iam.gserviceaccount.com"
gcloud pubsub topics add-iam-policy-binding gpu-point-requested-dlq \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.publisher" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding gpu-point-requested-sub \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.subscriber" --project=$PROJECT

# Reuses the SAME parcs-host/parcs-daemon/keda-operator SAs from §4 — just new bindings:
gcloud pubsub topics add-iam-policy-binding gpu-point-requested \
  --member="serviceAccount:parcs-host@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.publisher" --project=$PROJECT
gcloud pubsub topics add-iam-policy-binding gpu-point-requested \
  --member="serviceAccount:parcs-daemon@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.viewer" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding gpu-point-requested-sub \
  --member="serviceAccount:parcs-daemon@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.subscriber" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding gpu-point-requested-sub \
  --member="serviceAccount:keda-operator@${PROJECT}.iam.gserviceaccount.com" --role="roles/pubsub.viewer" --project=$PROJECT
```

Host also needs `PubSub__GpuTopicId=gpu-point-requested` in its env (already in
`kube/deployment.gcp.yaml`).

### ILGPU gotchas hit while fixing the branch's GPU modules

These cost real debugging time — if writing a new GPU module, check these first:

1. **`Context.CreateDefault()` doesn't expose `CreateCPUAccelerator`** — it's an
   extension method on `ILGPU.Runtime.CPU.CPUContextExtensions`. Add
   `using ILGPU.Runtime.CPU;` or the CPU-fallback branch of
   `context.GetCudaDevices().Count > 0 ? ... : context.CreateCPUAccelerator(0)`
   won't compile.
2. **`CudaDevice` has no `.CudaArchitecture` property** — it's `.Architecture`.
3. **XMath intrinsics (e.g. `XMath.Rsqrt`) need `Context.Builder.EnableAlgorithms()`**
   explicitly — `Context.Create(builder => builder.Default().EnableAlgorithms())`,
   not `Context.CreateDefault()`. Only needed if your kernel actually calls an
   `ILGPU.Algorithms.XMath` function; the three original GPU modules (Matrix
   multiply, Monte Carlo, Floyd-Warshall) don't need sqrt so this wasn't caught
   until the N-body module needed it.
4. **The GPU Dockerfile must copy the .NET runtime in, not apt-install it** —
   Microsoft's apt feed for Ubuntu 22.04 (jammy) doesn't have
   `dotnet-runtime-10.0` packages. Use a multi-stage `COPY --from=` from
   `mcr.microsoft.com/dotnet/runtime:10.0` instead (see `Dockerfile.gpu`).

### Module status (`modules/Parcs.Modules.*`)

| Module | Status |
|---|---|
| `MatrixesMultiplication` | GPU variant fixed (missing `using ILGPU.Runtime.CPU;`) and verified building. |
| `FloydWarshall` | **Correctness bug fixed**: `GpuWorkerModule.RunAsync` was launching a stub kernel (`RelaxRowsKernel`) that computed nothing and discarded its result — uploaded the matrix to GPU and downloaded it *unchanged*, every run. Fixed by calling the already-written (but previously dead) `RunRelaxKernel` helper instead. Not deployed/tested on a real GPU yet — only compile-verified. |
| `MonteCarloPi` | Was **never wired into the solution at all** (`.sln` didn't reference it) and didn't compile standalone: wrong `IChannel` API (`ReadDataAsync<T>` doesn't exist — real methods are `ReadIntAsync`/`ReadLongAsync`/etc.), `MonteCarloOptions` didn't implement `IModuleOptions`, targeted `net8.0`/`Parcs.Net 0.8.0` instead of `net10.0`/`10.0.0`. All fixed; added to `Parcs7.sln`. |
| `ProofOfWork` | GPU variant does not compile (references `ModuleOptions`/`ModuleOutput`/`IChannel` members that don't exist) and was explicitly descoped — excluded from compilation via `<Compile Remove="Gpu\**\*.cs" />` in its `.csproj` rather than fixed. |
| `NBody` (new) | Written from scratch for a GPU-vs-CPU paper benchmark: gravitational N-body simulation, gather/broadcast-per-step communication (same pattern as Floyd-Warshall, generalized — every worker needs every other body's position each step, not just one pivot row). Reports kinetic/potential/total energy as a correctness cross-check between the CPU and GPU variant for the same seed (should be close, not necessarily bit-identical). CPU-side correctness confirmed live: 64 bodies/2 workers/5 steps → `TotalEnergy ≈ -31.04` (negative = gravitationally bound system, physically correct). GPU variant compiles and is believed correct by code review but **not yet confirmed running on a real GPU** — every attempt so far hit infra blockers (§9, then real T4 stock exhaustion, see below) before actually executing on hardware. |

### Real-world GPU stock exhaustion (separate from quota and billing)

After fixing the billing-tier block (§9), node creation still failed, now with:

```
ZONE_RESOURCE_POOL_EXHAUSTED / GCE_STOCKOUT:
The zone 'europe-central2-b' does not have enough resources available to fulfill
the request. Try a different zone, or try again later.
```

This is a genuine, real-time regional T4 shortage — not something retrying our
own config fixes. We tried adding the other T4-capable zone
(`--node-locations=europe-central2-b,europe-central2-c`) via
`gcloud container node-pools update`; **this took over 35 minutes** (GKE was
internally retrying the new zone the whole time) and ended with the *same*
stockout in `-c` too — both zones were exhausted simultaneously. The node pool
was left in `RUNNING_WITH_ERROR` status after that; `gcloud container clusters
resize --num-nodes=0` cleared it back to a clean `RUNNING` state.

**Lesson:** `gcloud container node-pools update --node-locations=...` on a pool
that already has a pending scale-up can block the *entire cluster*
(`CLUSTER_ALREADY_HAS_OPERATION`) for the full duration of GKE's internal retry
loop, with no way to cancel it early. If you hit a stockout, it's faster to just
retry cluster-level resizes periodically (`gcloud container clusters resize
--num-nodes=1`, a few minutes apart) than to reconfigure node-locations while a
scale-up is already in flight.

**Options if you hit this again:** retry periodically in the same region (stock
frees up unpredictably); try a different region entirely (quota is identical
everywhere, but regional stock is independent — no latency concern for a batch
benchmark run, only for interactive Portal use); or try a different GPU type
(`nvidia-l4` also shows quota=1 in `europe-central2` and may have different
real-time availability than T4).

## 11. Full teardown (what we actually did, and why)

```bash
# 1. Delete k8s resources FIRST — LoadBalancer Services must go before the cluster
#    so their GCP forwarding rules get cleaned up (orphaned forwarding rules keep
#    billing even after the cluster is gone).
kubectl delete -f kube/deployment.gcp.yaml

# 2. Helm does NOT delete StatefulSet PVCs on uninstall (by design) — do it anyway,
#    then check for the orphaned disk afterward (see §6 gotcha).
helm uninstall nfs-server

# 3. Delete the cluster (both node pools, all node boot disks, in one call).
gcloud container clusters delete parcs-cluster --zone=europe-central2-a --project=$PROJECT --quiet

# 4. Delete Pub/Sub (CPU + GPU) — zero ongoing cost either way, deleted anyway for
#    a clean slate; recreation commands are in §3/§10 above.
for t in point-requested point-requested-dlq gpu-point-requested gpu-point-requested-dlq; do
  true # topics deleted individually: gcloud pubsub topics delete $t --project=$PROJECT --quiet
done
for s in point-requested-sub point-requested-dlq-sub gpu-point-requested-sub gpu-point-requested-dlq-sub; do
  true # subscriptions deleted individually: gcloud pubsub subscriptions delete $s --project=$PROJECT --quiet
done

# 5. Find and delete any orphaned PVC-backed disks (the NFS StatefulSet one from
#    step 2, plus any leftover from a previous region/zone — check `zone` on each
#    row against what you expect):
gcloud compute disks list --project=$PROJECT
gcloud compute disks delete <name> --zone=<zone> --project=$PROJECT --quiet
```

**Deliberately kept** (both effectively zero cost, and losing them means more
error-prone rework for no savings): the three IAM service accounts + Workload
Identity bindings (§4), and the Artifact Registry images (listed at the top of
this doc). Rebuilding/re-pushing images alone costs real Cloud Build + upload time
for no benefit if the source hasn't changed.

**Verify nothing billable is left:**
```bash
gcloud compute instances list --project=$PROJECT       # expect 0
gcloud compute disks list --project=$PROJECT            # expect 0 (or only ones you intend to keep)
gcloud container clusters list --project=$PROJECT       # expect empty
gcloud compute forwarding-rules list --project=$PROJECT # expect 0
```
