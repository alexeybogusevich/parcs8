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

@description('The name of the resource group.')
param resourceGroupName string

var agentCount = 3
var aksEngineVersion = 'v0.60.0'
var aksEngineDownloadUrl = 'https://github.com/Azure/aks-engine/releases/download/'
var aksEngineDownloadPath = '/aks-engine-v0.60.0-linux-amd64.tar.gz'
var kubeConfigFileName = 'kubeconfig.json'

resource aksCluster 'Microsoft.ContainerService/managedClusters@2023-01-02-preview' = {
  name: aksClusterName
  location: resourceGroupLocation
  properties: {
    kubernetesVersion: '1.25.2'
    enableRBAC: true
    agentPoolProfiles: [
      {
        name: 'agentpool'
        count: agentCount
        vmSize: 'Standard_B2s'
      }
    ]
  }
}

resource aksExtension 'Microsoft.Compute/virtualMachines/extensions@2022-11-01' = {
  name: '${aksCluster.name}/aksengine'
  location: resourceGroupLocation
  properties: {
    publisher: 'Microsoft.OSTCExtensions'
    type: 'CustomScriptForLinux'
    typeHandlerVersion: '1.9'
    autoUpgradeMinorVersion: true
    settings: {
      fileUris: [
        '${aksEngineDownloadUrl}${aksEngineVersion}${aksEngineDownloadPath}'
        'https://raw.githubusercontent.com/alexeybogusevich/parcs7/master/kube/deployment.yaml'
      ]
      commandToExecute: 'bash aks-engine-deploy.sh -g ${resourceGroupName} -c ${aksCluster.name} -f ${kubeConfigFileName}'
    }
  }
}
