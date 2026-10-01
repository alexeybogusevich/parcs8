# PARCS — Google Cloud Deployment Guide (CLI, manual, no Terraform)

This supersedes `parcs_gcp_guide.docx` (which only exists on the old `feature/GCloud`
branch and assumed the Cloud Console + a US region + the trial-credit project
`parcs-gcp`). This version documents the actual commands used to deploy PARCS to
GKE by hand via `gcloud` / `kubectl` / `helm`, with no Terraform/Bicep and no
Cloud Console clicking.

Applies to `kube/deployment.gcp.yaml` as it exists on `feature/GPU` — **CPU-only**,
no GPU node pool (the GPU work in this branch is still just a spec doc under
`docs/gpu_specification.docx`, not wired into the manifest).

## 0. Prerequisites

- `gcloud`, `kubectl`, `helm` installed and `gcloud auth login` done.
- A GCP project with **billing enabled**. Don't reuse a project whose billing was
  disabled (e.g. an old trial project) — every `container`/`artifactregistry` API
  call 403s with `BILLING_DISABLED` until it's re-linked, and Pub/Sub resources
  from a prior attempt can be left dangling (topics survive, subscriptions don't).
  Verify first: `gcloud billing projects describe <PROJECT_ID>`.
- Pick a region close to your users. Blazor Server (the Portal) round-trips every
  UI interaction over SignalR, so region choice directly affects how the UI feels,
  not just initial load time. For Ukraine, `europe-central2` (Warsaw) is GCP's
  closest region — meaningfully lower latency than the guide's original
  `us-central1` default (which was only chosen there for K80 GPU availability).

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
  --project=$PROJECT
```

Artifact Registry is **not needed** — `deployment.gcp.yaml` pulls prebuilt images
straight from Docker Hub (`oleksiibohusevych/parcshost`, `parcsdaemon`,
`parcsportal`, `parcs-agent-mcp`). Skip the image build/push section entirely
unless you're deploying your own fork's images.

Filestore API is also **not needed** — see the storage note in step 4.

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

**Gotcha not in the original guide:** dead-letter delivery silently doesn't work
unless the Pub/Sub service agent itself has publish rights on the DLQ topic and
subscribe rights on the source subscription. Grant these explicitly:

```bash
PROJECT_NUMBER=$(gcloud projects describe $PROJECT --format="value(projectNumber)")
PUBSUB_SA="service-${PROJECT_NUMBER}@gcp-sa-pubsub.iam.gserviceaccount.com"
gcloud pubsub topics add-iam-policy-binding point-requested-dlq \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.publisher" --project=$PROJECT
gcloud pubsub subscriptions add-iam-policy-binding point-requested-sub \
  --member="serviceAccount:${PUBSUB_SA}" --role="roles/pubsub.subscriber" --project=$PROJECT
```

## 4. Service accounts + IAM + Workload Identity

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

**Important:** `kube/deployment.gcp.yaml` hardcodes the GCP service-account emails
in the two `ServiceAccount` annotations (`parcs-daemon@...` / `parcs-host@...`)
rather than using a `PROJECT_ID` placeholder token. Before applying the manifest,
edit those two `iam.gke.io/gcp-service-account` annotation values to match your
actual project ID — there's nothing to `sed` automatically since it's not a
templated placeholder string.

These IAM/Workload Identity bindings and the Pub/Sub resources above are global —
they don't need to be recreated if you later rebuild the GKE cluster alone (e.g.
to move regions).

## 5. Install KEDA

```bash
kubectl apply --server-side --force-conflicts \
  -f https://github.com/kedacore/keda/releases/download/v2.12.0/keda-2.12.0.yaml
```

**Gotcha not in the original guide:** plain `kubectl apply -f` on this file fails
with `metadata.annotations: Too long: may not be more than 262144 bytes` — the
KEDA v2.12 CRDs (particularly `scaledjobs.keda.sh`) are too large to fit in the
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
production-scale deployment. Use the NFS-Ganesha chart instead, exactly as the
original guide recommended:

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

Open `http://<parcs-portal external IP>` for the Portal, and add the MCP server:

```bash
claude mcp add parcs --transport sse http://<parcs-agent-mcp-external external IP>:8080/sse
```

## 9. Cleanup / cost control

```bash
# Scale everything to zero without deleting config
kubectl scale deployment parcs-hostapi parcs-portal elasticsearch parcs-database --replicas=0

# Full teardown
helm uninstall nfs-server
kubectl delete -f kube/deployment.gcp.yaml   # deletes LoadBalancer Services first, so forwarding rules get cleaned up
gcloud container clusters delete parcs-cluster --zone=europe-central2-a --project=$PROJECT
```

Delete the manifest (and thus the `LoadBalancer` Services) *before* deleting the
cluster — otherwise the GCP forwarding rules backing those LoadBalancers can be
left orphaned and keep billing after the cluster is gone.
