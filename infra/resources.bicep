@allowed([
  'eastus'
  'eastus2'
  'centralus'
  'westus'
  'westus2'
  'northcentralus'
  'southcentralus'
  'westcentralus'
  'canadacentral'
  'canadaeast'
  'brazilsouth'
  'northeurope'
  'westeurope'
  'uksouth'
  'ukwest'
  'francecentral'
  'germanywestcentral'
  'norwayeast'
  'switzerlandnorth'
  'switzerlandwest'
  'norwaywest'
  'australiasoutheast'
  'australiaeast'
  'australiacentral'
  'australiacentral2'
  'japaneast'
  'japanwest'
  'koreacentral'
  'koreasouth'
  'southeastasia'
  'eastasia'
  'centralindia'
  'southindia'
  'westindia'
  'uaenorth'
  'uaecentral'
  'southafricanorth'
  'southafricawest'
])
@description('The location of the resourceGroup.')
param resourceGroupLocation string = 'eastus'

@description('The name of the AKS cluster.')
param aksClusterName string

var agentCount = 2          // Minimum nodes kept warm (host + baseline workload)
var agentMaxCount = 20      // Upper bound; raise if you need more than 20 concurrent daemons
var agentVMSize = 'Standard_DS2_v2'
var kubernetesVersion = '1.25.2'

// GPU node pool — used exclusively by parcs-daemon pods running GPU-accelerated modules.
//
// VM size: Standard_NC6 (6 vCPU, 56 GiB RAM, 1× NVIDIA K80 12 GiB VRAM).
// This is the only CUDA-capable family with non-zero quota in eastus:
//   Standard NCASv3_T4 Family vCPUs  →  quota 0  (T4, preferred — request an increase when ready)
//   Standard NC Family vCPUs         →  quota 12  (K80, used here)
//
// Capacity math:
//   12 vCPU quota ÷ 6 vCPU per NC6 = 2 nodes maximum.
//   Each node has 1 GPU; the NVIDIA device plugin exposes nvidia.com/gpu as an exclusive
//   resource, so exactly 1 daemon pod can run per node → 2 concurrent GPU daemons total.
//
// Set gpuMinCount to 0 so AKS scales the pool to zero when no GPU jobs are queued (cost saving).
// The pool is tainted with sku=gpu:NoSchedule so only daemon pods that explicitly tolerate it land here.
var gpuVMSize = 'Standard_NC6'
var gpuMinCount = 0
var gpuMaxCount = 2         // Hard ceiling: 2 nodes × 1 GPU each = 2 concurrent GPU daemons

resource aksCluster 'Microsoft.ContainerService/managedClusters@2021-07-01' = {
  name: aksClusterName
  location: resourceGroupLocation
  properties: {
    kubernetesVersion: kubernetesVersion
    enableRBAC: true
    agentPoolProfiles: [
      {
        name: 'agentpool'
        count: agentCount
        vmSize: agentVMSize
        // Cluster Autoscaler: scale out when daemon pods are Pending, scale in when idle
        enableAutoScaling: true
        minCount: agentCount
        maxCount: agentMaxCount
      }
      {
        // Dedicated GPU node pool for algorithmic modules that use ILGPU/CUDA acceleration.
        // AKS automatically installs the NVIDIA device plugin daemonset on GPU node pools,
        // which exposes nvidia.com/gpu as a schedulable resource on each node.
        name: 'gpupool'
        count: gpuMinCount
        vmSize: gpuVMSize
        enableAutoScaling: true
        minCount: gpuMinCount
        maxCount: gpuMaxCount
        mode: 'User'
        // Taint prevents non-GPU workloads from landing on expensive GPU nodes.
        // Only pods with a matching toleration (parcs-daemon ScaledJob) will be scheduled here.
        nodeTaints: [
          'sku=gpu:NoSchedule'
        ]
        nodeLabels: {
          accelerator: 'nvidia'
        }
      }
    ]
    // Autoscaler profile — tuned for ephemeral KEDA daemon jobs
    autoScalerProfile: {
      // Allow scale-down of nodes that host emptyDir / local-storage pods (daemon jobs use
      // the shared Azure Files PVC, not local storage, so this is safe to disable)
      'skip-nodes-with-local-storage': 'false'
      // How long a node must be idle before scale-down is considered
      'scale-down-unneeded-time': '5m'
      // How long to wait between scale-down evaluations
      'scale-down-delay-after-add': '5m'
      // Fraction of a node's allocatable CPU/memory that can be unallocated
      // while still allowing scale-down (default 0.5 — keep as-is)
      'scale-down-utilization-threshold': '0.5'
    }
  }
}
