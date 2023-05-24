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

@description('The name of the resource group.')
param resourceGroupName string

@description('The name of the AKS cluster.')
param aksClusterName string

targetScope = 'subscription'

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: resourceGroupName
  location: resourceGroupLocation
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: resourceGroup(resourceGroupName)
  params: {
    resourceGroupLocation: resourceGroupLocation
    aksClusterName: aksClusterName
  }
}
