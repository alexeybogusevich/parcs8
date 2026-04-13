terraform {
  required_version = ">= 1.6.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 5.0"
    }
  }

  # Uncomment to store state in GCS (recommended for teams)
  # backend "gcs" {
  #   bucket = "YOUR_TERRAFORM_STATE_BUCKET"
  #   prefix = "parcs/state"
  # }
}

# ---------------------------------------------------------------------------
# Variables
# ---------------------------------------------------------------------------

variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "Primary GCP region"
  type        = string
  default     = "us-central1"
}

variable "zone" {
  description = "Primary GCP zone (must support K80 GPUs)"
  type        = string
  default     = "us-central1-c"
}

variable "cluster_name" {
  description = "GKE cluster name"
  type        = string
  default     = "parcs-cluster"
}

variable "cpu_node_count" {
  description = "Initial node count for the default CPU node pool"
  type        = number
  default     = 3
}

variable "cpu_machine_type" {
  description = "Machine type for CPU worker nodes (≈ Standard_DS2_v2)"
  type        = string
  default     = "n1-standard-2"
}

variable "gpu_machine_type" {
  description = "Machine type for GPU nodes"
  type        = string
  default     = "n1-standard-4"
}

variable "gpu_type" {
  description = "GPU accelerator type"
  type        = string
  default     = "nvidia-tesla-k80"
}

variable "gpu_count_per_node" {
  description = "Number of GPUs per node in the GPU pool"
  type        = number
  default     = 1
}

variable "gpu_node_max_count" {
  description = "Maximum nodes in the GPU pool (quota-constrained)"
  type        = number
  default     = 2
}

variable "artifact_registry_location" {
  description = "Artifact Registry repository location"
  type        = string
  default     = "us-central1"
}

variable "pubsub_topic_name" {
  description = "Pub/Sub topic name for point creation requests"
  type        = string
  default     = "point-requested"
}

variable "pubsub_subscription_name" {
  description = "Pub/Sub subscription name consumed by KEDA + daemon pods"
  type        = string
  default     = "point-requested-sub"
}

variable "keda_namespace" {
  description = "Kubernetes namespace where KEDA is installed"
  type        = string
  default     = "keda"
}

variable "parcs_namespace" {
  description = "Kubernetes namespace for PARCS workloads"
  type        = string
  default     = "default"
}

# ---------------------------------------------------------------------------
# Providers
# ---------------------------------------------------------------------------

provider "google" {
  project = var.project_id
  region  = var.region
  zone    = var.zone
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
  zone    = var.zone
}

# ---------------------------------------------------------------------------
# Enable required APIs
# ---------------------------------------------------------------------------

resource "google_project_service" "container" {
  service            = "container.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "pubsub" {
  service            = "pubsub.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "artifactregistry" {
  service            = "artifactregistry.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "iam" {
  service            = "iam.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "file" {
  # Cloud Filestore — required for ReadWriteMany PVCs
  service            = "file.googleapis.com"
  disable_on_destroy = false
}

# ---------------------------------------------------------------------------
# VPC network (simple; add subnets/NAT for production hardening)
# ---------------------------------------------------------------------------

resource "google_compute_network" "parcs_vpc" {
  name                    = "parcs-vpc"
  auto_create_subnetworks = false
  depends_on              = [google_project_service.container]
}

resource "google_compute_subnetwork" "parcs_subnet" {
  name          = "parcs-subnet"
  ip_cidr_range = "10.0.0.0/20"
  region        = var.region
  network       = google_compute_network.parcs_vpc.id

  secondary_ip_range {
    range_name    = "pods"
    ip_cidr_range = "10.1.0.0/16"
  }

  secondary_ip_range {
    range_name    = "services"
    ip_cidr_range = "10.2.0.0/20"
  }
}

# ---------------------------------------------------------------------------
# GKE cluster
# ---------------------------------------------------------------------------

resource "google_container_cluster" "parcs" {
  name     = var.cluster_name
  location = var.zone   # zonal cluster; use var.region for regional HA

  # We manage node pools separately; remove the default pool.
  remove_default_node_pool = true
  initial_node_count       = 1

  network    = google_compute_network.parcs_vpc.id
  subnetwork = google_compute_subnetwork.parcs_subnet.id

  ip_allocation_policy {
    cluster_secondary_range_name  = "pods"
    services_secondary_range_name = "services"
  }

  workload_identity_config {
    workload_pool = "${var.project_id}.svc.id.goog"
  }

  addons_config {
    gce_persistent_disk_csi_driver_config {
      enabled = true
    }
    gcs_fuse_csi_driver_config {
      enabled = false
    }
  }

  # Filestore CSI driver (needed for ReadWriteMany PVCs)
  addons_config {
    gcp_filestore_csi_driver_config {
      enabled = true
    }
  }

  release_channel {
    channel = "REGULAR"
  }

  maintenance_policy {
    recurring_window {
      start_time = "2024-01-01T03:00:00Z"
      end_time   = "2024-01-01T07:00:00Z"
      recurrence = "FREQ=WEEKLY;BYDAY=SA"
    }
  }

  depends_on = [google_project_service.container]
}

# ---------------------------------------------------------------------------
# CPU node pool  (always-on baseline; 3 nodes by default)
# ---------------------------------------------------------------------------

resource "google_container_node_pool" "cpu_pool" {
  name       = "cpu-pool"
  cluster    = google_container_cluster.parcs.id
  node_count = var.cpu_node_count

  autoscaling {
    min_node_count = var.cpu_node_count
    max_node_count = var.cpu_node_count * 3
  }

  node_config {
    machine_type = var.cpu_machine_type
    disk_size_gb = 50
    disk_type    = "pd-standard"

    oauth_scopes = [
      "https://www.googleapis.com/auth/cloud-platform",
    ]

    workload_metadata_config {
      mode = "GKE_METADATA"
    }

    labels = {
      pool = "cpu"
    }

    metadata = {
      disable-legacy-endpoints = "true"
    }
  }

  management {
    auto_repair  = true
    auto_upgrade = true
  }
}

# ---------------------------------------------------------------------------
# GPU node pool  (autoscaled 0 → gpu_node_max_count; K80 GPUs)
# ---------------------------------------------------------------------------

resource "google_container_node_pool" "gpu_pool" {
  provider = google-beta

  name    = "gpu-pool"
  cluster = google_container_cluster.parcs.id

  # Start at 0; KEDA/cluster-autoscaler scales up on demand.
  initial_node_count = 0

  autoscaling {
    min_node_count = 0
    max_node_count = var.gpu_node_max_count
  }

  node_config {
    machine_type = var.gpu_machine_type
    disk_size_gb = 100
    disk_type    = "pd-ssd"

    guest_accelerator {
      type  = var.gpu_type
      count = var.gpu_count_per_node

      # Auto-install NVIDIA drivers (recommended over DaemonSet for GKE)
      gpu_driver_installation_config {
        gpu_driver_version = "LATEST"
      }
    }

    oauth_scopes = [
      "https://www.googleapis.com/auth/cloud-platform",
    ]

    workload_metadata_config {
      mode = "GKE_METADATA"
    }

    labels = {
      pool        = "gpu"
      accelerator = "nvidia"
    }

    taint {
      key    = "sku"
      value  = "gpu"
      effect = "NO_SCHEDULE"
    }

    metadata = {
      disable-legacy-endpoints = "true"
    }
  }

  management {
    auto_repair  = true
    auto_upgrade = true
  }
}

# ---------------------------------------------------------------------------
# Google Artifact Registry  (Docker repository for PARCS images)
# ---------------------------------------------------------------------------

resource "google_artifact_registry_repository" "parcs" {
  location      = var.artifact_registry_location
  repository_id = "parcs"
  format        = "DOCKER"
  description   = "PARCS container images"

  depends_on = [google_project_service.artifactregistry]
}

# ---------------------------------------------------------------------------
# Pub/Sub  (replaces Azure Service Bus)
# ---------------------------------------------------------------------------

resource "google_pubsub_topic" "point_requested" {
  name = var.pubsub_topic_name

  message_retention_duration = "86400s"   # 24 h

  depends_on = [google_project_service.pubsub]
}

resource "google_pubsub_subscription" "point_requested_sub" {
  name  = var.pubsub_subscription_name
  topic = google_pubsub_topic.point_requested.id

  # Keep messages for up to 10 minutes if not ACKed (KEDA reads pending count).
  ack_deadline_seconds       = 60
  message_retention_duration = "600s"    # 10 min — short-lived work items

  # Dead-letter after 5 failed delivery attempts.
  dead_letter_policy {
    dead_letter_topic     = google_pubsub_topic.point_requested_dlq.id
    max_delivery_attempts = 5
  }

  retry_policy {
    minimum_backoff = "10s"
    maximum_backoff = "60s"
  }

  depends_on = [google_pubsub_topic.point_requested]
}

resource "google_pubsub_topic" "point_requested_dlq" {
  name = "${var.pubsub_topic_name}-dlq"

  depends_on = [google_project_service.pubsub]
}

resource "google_pubsub_subscription" "point_requested_dlq_sub" {
  name  = "${var.pubsub_subscription_name}-dlq"
  topic = google_pubsub_topic.point_requested_dlq.id

  ack_deadline_seconds = 600
}

# ---------------------------------------------------------------------------
# Service accounts
# ---------------------------------------------------------------------------

# PARCS Host SA — publishes to Pub/Sub topic
resource "google_service_account" "parcs_host" {
  account_id   = "parcs-host"
  display_name = "PARCS Host Service Account"
}

# PARCS Daemon SA — subscribes to Pub/Sub (and is also used by KEDA to read
# subscription metrics via Workload Identity)
resource "google_service_account" "parcs_daemon" {
  account_id   = "parcs-daemon"
  display_name = "PARCS Daemon Service Account"
}

# KEDA operator SA — reads subscription message counts for scaling decisions
resource "google_service_account" "keda_operator" {
  account_id   = "keda-operator"
  display_name = "KEDA Operator Service Account"
}

# ---------------------------------------------------------------------------
# IAM bindings
# ---------------------------------------------------------------------------

# Host can publish
resource "google_pubsub_topic_iam_member" "host_publisher" {
  topic  = google_pubsub_topic.point_requested.id
  role   = "roles/pubsub.publisher"
  member = "serviceAccount:${google_service_account.parcs_host.email}"
}

# Daemon can subscribe (consume messages)
resource "google_pubsub_subscription_iam_member" "daemon_subscriber" {
  subscription = google_pubsub_subscription.point_requested_sub.id
  role         = "roles/pubsub.subscriber"
  member       = "serviceAccount:${google_service_account.parcs_daemon.email}"
}

# Daemon needs viewer to ACK/NACK (subscriber role covers this, but make explicit)
resource "google_pubsub_topic_iam_member" "daemon_viewer" {
  topic  = google_pubsub_topic.point_requested.id
  role   = "roles/pubsub.viewer"
  member = "serviceAccount:${google_service_account.parcs_daemon.email}"
}

# KEDA needs to read subscription metadata (message count) for scaling
resource "google_pubsub_subscription_iam_member" "keda_viewer" {
  subscription = google_pubsub_subscription.point_requested_sub.id
  role         = "roles/pubsub.viewer"
  member       = "serviceAccount:${google_service_account.keda_operator.email}"
}

resource "google_project_iam_member" "keda_monitoring_viewer" {
  project = var.project_id
  role    = "roles/monitoring.viewer"
  member  = "serviceAccount:${google_service_account.keda_operator.email}"
}

# ---------------------------------------------------------------------------
# Workload Identity bindings
# (K8s ServiceAccount  <-->  GCP ServiceAccount)
# ---------------------------------------------------------------------------

# parcs-host K8s SA → parcs-host GCP SA
resource "google_service_account_iam_member" "host_workload_identity" {
  service_account_id = google_service_account.parcs_host.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "serviceAccount:${var.project_id}.svc.id.goog[${var.parcs_namespace}/parcs-host]"
}

# parcs-daemon K8s SA → parcs-daemon GCP SA
resource "google_service_account_iam_member" "daemon_workload_identity" {
  service_account_id = google_service_account.parcs_daemon.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "serviceAccount:${var.project_id}.svc.id.goog[${var.parcs_namespace}/parcs-daemon]"
}

# keda-operator K8s SA → keda-operator GCP SA
resource "google_service_account_iam_member" "keda_workload_identity" {
  service_account_id = google_service_account.keda_operator.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "serviceAccount:${var.project_id}.svc.id.goog[${var.keda_namespace}/keda-operator]"
}

# ---------------------------------------------------------------------------
# Outputs
# ---------------------------------------------------------------------------

output "cluster_name" {
  description = "GKE cluster name"
  value       = google_container_cluster.parcs.name
}

output "cluster_endpoint" {
  description = "GKE cluster API endpoint"
  value       = google_container_cluster.parcs.endpoint
  sensitive   = true
}

output "artifact_registry_url" {
  description = "Docker push URL: docker push <url>/IMAGE:TAG"
  value       = "${var.artifact_registry_location}-docker.pkg.dev/${var.project_id}/parcs"
}

output "pubsub_topic" {
  value = google_pubsub_topic.point_requested.id
}

output "pubsub_subscription" {
  value = google_pubsub_subscription.point_requested_sub.id
}

output "parcs_host_sa_email" {
  value = google_service_account.parcs_host.email
}

output "parcs_daemon_sa_email" {
  value = google_service_account.parcs_daemon.email
}

output "keda_operator_sa_email" {
  value = google_service_account.keda_operator.email
}

output "get_credentials_command" {
  description = "Run this to configure kubectl after apply"
  value       = "gcloud container clusters get-credentials ${var.cluster_name} --zone ${var.zone} --project ${var.project_id}"
}
