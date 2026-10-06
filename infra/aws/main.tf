# PARCS on Amazon EKS
#
# AWS counterpart of infra/main.bicep (AKS) and infra/gcp/main.tf (GKE):
#
#   Point queue        Service Bus / Pub/Sub   →  SQS (+ dead-letter queues)
#   Pod identity       Azure WI / GKE WI       →  IRSA (IAM Roles for Service Accounts)
#   Node autoscaling   AKS/GKE Cluster Autosc. →  Karpenter (provisions EC2 per pending pod)
#   RWX storage        Azure Files / Filestore →  EFS (+ EFS CSI driver)
#   RWO storage        managed-csi / PD        →  EBS gp3 (+ EBS CSI driver)
#   Registry           ACR / Artifact Registry →  ECR
#
# After `terraform apply`:
#   aws eks update-kubeconfig --name <cluster_name> --region <region>
#   kubectl apply -f kube/aws-karpenter.yaml   # Karpenter NodePools (CPU + GPU)
#   kubectl apply -f kube/deployment.aws.yaml  # PARCS workloads
#
# See docs/aws-deployment-guide.md for the full walkthrough.

terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.80"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.16"
    }
  }

  # Uncomment to store state in S3 (recommended for teams)
  # backend "s3" {
  #   bucket = "YOUR_TERRAFORM_STATE_BUCKET"
  #   key    = "parcs/terraform.tfstate"
  #   region = "eu-central-1"
  # }
}

# ---------------------------------------------------------------------------
# Variables
# ---------------------------------------------------------------------------

variable "region" {
  description = "AWS region"
  type        = string
  default     = "eu-central-1"
}

variable "cluster_name" {
  description = "EKS cluster name"
  type        = string
  default     = "parcs-cluster"
}

variable "kubernetes_version" {
  description = "EKS Kubernetes version"
  type        = string
  default     = "1.31"
}

variable "system_instance_type" {
  description = "Instance type of the always-on node group (Host, Portal, Postgres, Elasticsearch, KEDA, Karpenter)"
  type        = string
  default     = "m5.large"
}

variable "system_node_count" {
  description = "Size of the always-on node group"
  type        = number
  default     = 2
}

variable "sqs_queue_name" {
  description = "SQS queue for CPU point-creation requests"
  type        = string
  default     = "point-requested"
}

variable "sqs_gpu_queue_name" {
  description = "SQS queue for GPU point-creation requests"
  type        = string
  default     = "gpu-point-requested"
}

variable "parcs_namespace" {
  description = "Kubernetes namespace for PARCS workloads"
  type        = string
  default     = "default"
}

variable "keda_namespace" {
  description = "Kubernetes namespace for KEDA"
  type        = string
  default     = "keda"
}

variable "karpenter_version" {
  description = "Karpenter Helm chart version"
  type        = string
  default     = "1.1.1"
}

variable "keda_version" {
  description = "KEDA Helm chart version"
  type        = string
  default     = "2.16.1"
}

# ---------------------------------------------------------------------------
# Providers
# ---------------------------------------------------------------------------

provider "aws" {
  region = var.region
}

# Karpenter's chart is published to ECR Public, which is only served from us-east-1.
provider "aws" {
  alias  = "virginia"
  region = "us-east-1"
}

data "aws_ecrpublic_authorization_token" "token" {
  provider = aws.virginia
}

data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_caller_identity" "current" {}

provider "helm" {
  kubernetes {
    host                   = module.eks.cluster_endpoint
    cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)

    exec {
      api_version = "client.authentication.k8s.io/v1beta1"
      command     = "aws"
      args        = ["eks", "get-token", "--cluster-name", module.eks.cluster_name, "--region", var.region]
    }
  }
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, 3)

  tags = {
    Project = "parcs"
  }
}

# ---------------------------------------------------------------------------
# Network
# ---------------------------------------------------------------------------

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.16"

  name = "parcs-vpc"
  cidr = "10.0.0.0/16"

  azs             = local.azs
  private_subnets = ["10.0.0.0/19", "10.0.32.0/19", "10.0.64.0/19"]
  public_subnets  = ["10.0.96.0/22", "10.0.100.0/22", "10.0.104.0/22"]

  enable_nat_gateway = true
  single_nat_gateway = true # one NAT keeps the research cluster cheap; use one per AZ for HA

  public_subnet_tags = {
    "kubernetes.io/role/elb" = 1
  }

  private_subnet_tags = {
    "kubernetes.io/role/internal-elb" = 1
    # Karpenter discovers the subnets it may launch nodes into by this tag.
    "karpenter.sh/discovery" = var.cluster_name
  }

  tags = local.tags
}

# ---------------------------------------------------------------------------
# EKS cluster
# ---------------------------------------------------------------------------

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.31"

  cluster_name    = var.cluster_name
  cluster_version = var.kubernetes_version

  cluster_endpoint_public_access           = true
  enable_cluster_creator_admin_permissions = true

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  cluster_addons = {
    coredns                = {}
    kube-proxy             = {}
    vpc-cni                = {}
    eks-pod-identity-agent = {}
    aws-ebs-csi-driver = {
      service_account_role_arn = module.ebs_csi_irsa.iam_role_arn
    }
    aws-efs-csi-driver = {
      service_account_role_arn = module.efs_csi_irsa.iam_role_arn
    }
  }

  # Always-on capacity for the control-plane workloads. Daemon pods do not run here: they
  # land on nodes Karpenter launches on demand (see kube/aws-karpenter.yaml).
  eks_managed_node_groups = {
    system = {
      instance_types = [var.system_instance_type]
      min_size       = var.system_node_count
      max_size       = var.system_node_count
      desired_size   = var.system_node_count

      labels = {
        pool = "system"
      }
    }
  }

  node_security_group_tags = {
    "karpenter.sh/discovery" = var.cluster_name
  }

  tags = local.tags
}

# ---------------------------------------------------------------------------
# Karpenter  (node autoscaling; replaces AKS/GKE Cluster Autoscaler)
#
# Cluster Autoscaler grows fixed node groups; Karpenter instead launches an EC2 instance
# sized for the pending pods directly, which shortens the "new point → new node" path that
# dominates PARCS provisioning latency.
# ---------------------------------------------------------------------------

module "karpenter" {
  source  = "terraform-aws-modules/eks/aws//modules/karpenter"
  version = "~> 20.31"

  cluster_name = module.eks.cluster_name

  enable_v1_permissions           = true
  enable_pod_identity             = true
  create_pod_identity_association = true

  # Fixed name: kube/aws-karpenter.yaml references it in the EC2NodeClass.
  node_iam_role_use_name_prefix = false
  node_iam_role_name            = "KarpenterNodeRole-${var.cluster_name}"

  node_iam_role_additional_policies = {
    AmazonSSMManagedInstanceCore = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
  }

  tags = local.tags
}

resource "helm_release" "karpenter" {
  namespace           = "kube-system"
  name                = "karpenter"
  repository          = "oci://public.ecr.aws/karpenter"
  repository_username = data.aws_ecrpublic_authorization_token.token.user_name
  repository_password = data.aws_ecrpublic_authorization_token.token.password
  chart               = "karpenter"
  version             = var.karpenter_version
  wait                = false

  values = [
    <<-EOT
    nodeSelector:
      pool: system
    settings:
      clusterName: ${module.eks.cluster_name}
      clusterEndpoint: ${module.eks.cluster_endpoint}
      interruptionQueue: ${module.karpenter.queue_name}
    EOT
  ]
}

# ---------------------------------------------------------------------------
# SQS  (point queue; replaces Service Bus / Pub/Sub)
# ---------------------------------------------------------------------------

resource "aws_sqs_queue" "point_requested_dlq" {
  name                      = "${var.sqs_queue_name}-dlq"
  message_retention_seconds = 86400
  tags                      = local.tags
}

resource "aws_sqs_queue" "point_requested" {
  name = var.sqs_queue_name

  # Point requests are short-lived work items; an unconsumed one is stale after 10 minutes.
  message_retention_seconds = 600
  # Daemons extend visibility while a point runs (SqsConfiguration.VisibilityTimeoutSeconds).
  visibility_timeout_seconds = 120
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.point_requested_dlq.arn
    maxReceiveCount     = 5
  })

  tags = local.tags
}

resource "aws_sqs_queue" "gpu_point_requested_dlq" {
  name                      = "${var.sqs_gpu_queue_name}-dlq"
  message_retention_seconds = 86400
  tags                      = local.tags
}

resource "aws_sqs_queue" "gpu_point_requested" {
  name                       = var.sqs_gpu_queue_name
  message_retention_seconds  = 600
  visibility_timeout_seconds = 120
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.gpu_point_requested_dlq.arn
    maxReceiveCount     = 5
  })

  tags = local.tags
}

# ---------------------------------------------------------------------------
# IAM for workloads (IRSA)
# ---------------------------------------------------------------------------

locals {
  point_queue_arns = [aws_sqs_queue.point_requested.arn, aws_sqs_queue.gpu_point_requested.arn]
}

resource "aws_iam_policy" "parcs_host" {
  name = "${var.cluster_name}-parcs-host"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["sqs:SendMessage", "sqs:GetQueueUrl", "sqs:GetQueueAttributes"]
      Resource = local.point_queue_arns
    }]
  })
}

# Daemons consume their own point request and publish requests for nested points.
resource "aws_iam_policy" "parcs_daemon" {
  name = "${var.cluster_name}-parcs-daemon"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:ChangeMessageVisibility",
        "sqs:SendMessage",
        "sqs:GetQueueUrl",
        "sqs:GetQueueAttributes",
      ]
      Resource = local.point_queue_arns
    }]
  })
}

# KEDA's aws-sqs-queue scaler reads ApproximateNumberOfMessages(+NotVisible).
resource "aws_iam_policy" "keda_operator" {
  name = "${var.cluster_name}-keda-operator"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["sqs:GetQueueAttributes", "sqs:GetQueueUrl"]
      Resource = local.point_queue_arns
    }]
  })
}

module "parcs_host_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.48"

  role_name = "${var.cluster_name}-parcs-host"
  role_policy_arns = {
    sqs = aws_iam_policy.parcs_host.arn
  }

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["${var.parcs_namespace}:parcs-host"]
    }
  }
}

module "parcs_daemon_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.48"

  role_name = "${var.cluster_name}-parcs-daemon"
  role_policy_arns = {
    sqs = aws_iam_policy.parcs_daemon.arn
  }

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["${var.parcs_namespace}:parcs-daemon"]
    }
  }
}

module "keda_operator_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.48"

  role_name = "${var.cluster_name}-keda-operator"
  role_policy_arns = {
    sqs = aws_iam_policy.keda_operator.arn
  }

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["${var.keda_namespace}:keda-operator"]
    }
  }
}

module "ebs_csi_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.48"

  role_name             = "${var.cluster_name}-ebs-csi"
  attach_ebs_csi_policy = true

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["kube-system:ebs-csi-controller-sa"]
    }
  }
}

module "efs_csi_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.48"

  role_name             = "${var.cluster_name}-efs-csi"
  attach_efs_csi_policy = true

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["kube-system:efs-csi-controller-sa", "kube-system:efs-csi-node-sa"]
    }
  }
}

# ---------------------------------------------------------------------------
# KEDA
# ---------------------------------------------------------------------------

resource "helm_release" "keda" {
  namespace        = var.keda_namespace
  create_namespace = true
  name             = "keda"
  repository       = "https://kedacore.github.io/charts"
  chart            = "keda"
  version          = var.keda_version

  values = [
    <<-EOT
    nodeSelector:
      pool: system
    podIdentity:
      aws:
        irsa:
          enabled: true
          roleArn: ${module.keda_operator_irsa.iam_role_arn}
    EOT
  ]

  depends_on = [module.eks]
}

# ---------------------------------------------------------------------------
# EFS  (shared ReadWriteMany storage for modules, job inputs/outputs, datasets)
# ---------------------------------------------------------------------------

resource "aws_efs_file_system" "parcs" {
  creation_token   = "${var.cluster_name}-storage"
  encrypted        = true
  performance_mode = "generalPurpose"
  throughput_mode  = "elastic"

  tags = merge(local.tags, { Name = "${var.cluster_name}-storage" })
}

resource "aws_security_group" "efs" {
  name        = "${var.cluster_name}-efs"
  description = "NFS from EKS nodes"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "NFS"
    from_port       = 2049
    to_port         = 2049
    protocol        = "tcp"
    security_groups = [module.eks.node_security_group_id]
  }

  tags = local.tags
}

resource "aws_efs_mount_target" "parcs" {
  count = length(module.vpc.private_subnets)

  file_system_id  = aws_efs_file_system.parcs.id
  subnet_id       = module.vpc.private_subnets[count.index]
  security_groups = [aws_security_group.efs.id]
}

# ---------------------------------------------------------------------------
# ECR  (container images)
# ---------------------------------------------------------------------------

resource "aws_ecr_repository" "parcs" {
  for_each = toset(["parcshost", "parcsdaemon", "parcsdaemon-gpu", "parcsportal", "parcs-agent-mcp"])

  name                 = "parcs/${each.key}"
  image_tag_mutability = "MUTABLE"
  force_delete         = true

  tags = local.tags
}

# ---------------------------------------------------------------------------
# Outputs  (values to substitute into kube/deployment.aws.yaml and kube/aws-karpenter.yaml)
# ---------------------------------------------------------------------------

output "cluster_name" {
  value = module.eks.cluster_name
}

output "region" {
  value = var.region
}

output "sqs_queue_url" {
  value = aws_sqs_queue.point_requested.url
}

output "sqs_gpu_queue_url" {
  value = aws_sqs_queue.gpu_point_requested.url
}

output "parcs_host_role_arn" {
  value = module.parcs_host_irsa.iam_role_arn
}

output "parcs_daemon_role_arn" {
  value = module.parcs_daemon_irsa.iam_role_arn
}

output "efs_file_system_id" {
  value = aws_efs_file_system.parcs.id
}

output "karpenter_node_role_name" {
  value = module.karpenter.node_iam_role_name
}

output "ecr_registry" {
  description = "docker push <ecr_registry>/parcs/IMAGE:TAG"
  value       = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.region}.amazonaws.com"
}
