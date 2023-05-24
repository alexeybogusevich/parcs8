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

var agentCount = 1
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
      }
    ]
  }
}
