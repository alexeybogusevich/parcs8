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
