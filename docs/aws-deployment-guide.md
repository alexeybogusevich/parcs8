# PARCS on AWS (EKS) — Deployment Guide

AWS counterpart of [gcp-deployment-guide.md](gcp-deployment-guide.md). The application code is
the same on every cloud; only the point-queue provider and the infrastructure differ.

| Concern | Azure (AKS) | GCP (GKE) | AWS (EKS) |
|---|---|---|---|
| Point queue | Service Bus | Pub/Sub | **SQS** |
| KEDA scaler | `azure-servicebus` | `gcp-pubsub` | **`aws-sqs-queue`** |
| Pod identity | connection-string secret | Workload Identity | **IRSA** |
| Node autoscaling | Cluster Autoscaler | Cluster Autoscaler | **Karpenter** |
| Shared (RWX) storage | Azure Files | NFS / Filestore | **EFS** |
| GPU nodes | NC-series pool | T4 pool | **g4dn / g5 / g6 via Karpenter** |
| IaC | `infra/main.bicep` | `infra/gcp/main.tf` | **`infra/aws/main.tf`** |
| Manifest | `kube/deployment.azure.yaml` | `kube/deployment.gcp.yaml` | **`kube/deployment.aws.yaml`** + `kube/aws-karpenter.yaml` |

The provider is selected by `PointQueue__Provider` (`PubSub` | `ServiceBus` | `Sqs`); see
`src/Parcs.Core/Messaging/`.

## Prerequisites

- AWS CLI v2 configured (`aws sts get-caller-identity` works), Terraform ≥ 1.6, kubectl, Docker, `envsubst`.
- For GPU daemons: a non-zero **"Running On-Demand G and VT instances"** vCPU quota in the
  target region (Service Quotas console). A g4dn.xlarge needs 4 vCPU of that quota.

## 1. Infrastructure

```bash
cd infra/aws
cp terraform.tfvars.example terraform.tfvars   # adjust region / sizes
terraform init
terraform apply
```

Creates: VPC (3 AZs, single NAT), EKS with a 2-node `system` group, Karpenter, KEDA (with an
IRSA role for the SQS scaler), SQS queues `point-requested` / `gpu-point-requested` with
dead-letter queues, IRSA roles for Host and Daemon, EFS + EBS CSI drivers, ECR repositories.

```bash
aws eks update-kubeconfig --name $(terraform output -raw cluster_name) --region $(terraform output -raw region)
cd ../..
kubectl apply -f kube/aws-karpenter.yaml
```

## 2. Images

```bash
export ECR_REGISTRY=$(terraform -chdir=infra/aws output -raw ecr_registry)
aws ecr get-login-password --region $(terraform -chdir=infra/aws output -raw region) \
  | docker login --username AWS --password-stdin $ECR_REGISTRY

# Build context is the repo root (the Dockerfiles COPY src/<Project>/...)
docker build -f src/Parcs.Host/Dockerfile        -t $ECR_REGISTRY/parcs/parcshost:latest       .
docker build -f src/Parcs.Daemon/Dockerfile      -t $ECR_REGISTRY/parcs/parcsdaemon:latest     .
docker build -f src/Parcs.Daemon/Dockerfile.gpu  -t $ECR_REGISTRY/parcs/parcsdaemon-gpu:latest .
docker build -f src/Parcs.Portal/Dockerfile      -t $ECR_REGISTRY/parcs/parcsportal:latest     .
docker build -f src/Parcs.Agent.Mcp/Dockerfile   -t $ECR_REGISTRY/parcs/parcs-agent-mcp:latest .
for i in parcshost parcsdaemon parcsdaemon-gpu parcsportal parcs-agent-mcp; do docker push $ECR_REGISTRY/parcs/$i:latest; done
```

## 3. Workloads

```bash
kubectl create secret generic parcs-database-secret --from-literal=password='<choose-a-password>'

export AWS_REGION=$(terraform -chdir=infra/aws output -raw region)
export SQS_QUEUE_URL=$(terraform -chdir=infra/aws output -raw sqs_queue_url)
export SQS_GPU_QUEUE_URL=$(terraform -chdir=infra/aws output -raw sqs_gpu_queue_url)
export PARCS_HOST_ROLE_ARN=$(terraform -chdir=infra/aws output -raw parcs_host_role_arn)
export PARCS_DAEMON_ROLE_ARN=$(terraform -chdir=infra/aws output -raw parcs_daemon_role_arn)
export EFS_FILE_SYSTEM_ID=$(terraform -chdir=infra/aws output -raw efs_file_system_id)

envsubst < kube/deployment.aws.yaml | kubectl apply -f -
kubectl get svc parcs-portal parcs-hostapi parcs-agent-mcp-external   # wait for EXTERNAL-IP
```

## 4. Verify scaling

```bash
# Run any module with N points from the Portal, then watch:
kubectl get scaledjob,jobs,pods -w
kubectl get nodeclaims -w            # Karpenter launching nodes for pending daemons
```

The Host logs `All N daemons connected for job J in Xs` and each daemon logs
`Point request ... picked up Ys after it was published` — the provisioning-latency figures used
in the scaling experiments.

## Teardown

```bash
envsubst < kube/deployment.aws.yaml | kubectl delete -f -   # releases load balancers and EBS volumes
kubectl delete -f kube/aws-karpenter.yaml                    # Karpenter drains and terminates its nodes
terraform -chdir=infra/aws destroy
```

Delete the workloads first: AWS load balancers and Karpenter-launched instances are not tracked
by Terraform and would otherwise block VPC deletion.
