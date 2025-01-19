# PARCS-NET-K8

## Overview
PARCS-NET-K8 is a solution for deploying and managing algorithmic modules that solve recursive parallel computation problems. This repository contains the necessary files and configurations to deploy the solution onto Azure as an AKS (Azure Kubernetes Service) service. It also provides a local development environment using Docker Compose.

## Deployment

To deploy the solution onto Azure as an AKS service, follow these steps:

1. Create a Kubernetes cluster (AKS) on Azure using the Azure Portal. This will provision the necessary resources for the AKS cluster. Ensure the node pool is configured with a minimum image size of **DS2_v2** General Purpose to meet performance and resource requirements.

2. **Adjust Resource Limits**:  
   The resource limits for CPU and memory in the YAML configuration should be adjusted based on the chosen VM size for your AKS cluster. For example, if you select a larger VM size (e.g., **Standard_D4_v5** or **Standard_D8_v5**), increase the resource requests and limits for your pods accordingly to ensure optimal performance. 

3. **Adjust the Number of Daemons**:  
   You can adjust the number of **parcs-daemon** replicas based on your cluster size and the computational load. If your workload requires more parallel processing, increase the number of daemons to scale horizontally and distribute the computation more effectively.

4. Apply the YAML file located at `kube/deployment.azure.yaml` to configure the AKS cluster. This file specifies the desired state of the cluster and sets up the necessary configurations.

## Local Development
For local development and debugging, you can use Docker Compose. Follow these steps:

1. Ensure you have Docker Compose installed on your local machine.

2. Use the `src/docker-compose.yml` file to set up the local development environment. This file defines the necessary services and their configurations.

3. Build and run the Docker Compose setup using the following command:
   ```
   docker-compose up
   ```

   This will create the required containers and start the local development environment.

## NuGet Package
The solution depends on a NuGet package named "Parcs.Net" (version 4.0.0). You can find the package on NuGet.org at the following URL: [https://www.nuget.org/packages/Parcs.Net/](https://www.nuget.org/packages/Parcs.Net/)

Make sure to include this package in your project to utilize the algorithmic modules provided by the solution.

## Exploring Logs in Kibana
PARCS logs, including logs from the **parcs-daemon** and **parcs-hostapi**, are stored in **Elasticsearch** and can be explored using **Kibana**. To view and analyze these logs, you need to set up a **data view** in Kibana that points to the appropriate Elasticsearch index.

### Steps to Explore Logs in Kibana

1. **Access Kibana**: 
   - After deploying the solution, you can access the Kibana dashboard via the exposed Kibana service on port 5601. If using AKS, you can access it using the LoadBalancer IP or DNS name.
   
2. **Create a Data View in Kibana**:
   - In Kibana, navigate to **"Stack Management"** > **"Data Views"**.
   - Click on **"Create data view"**.
   - Enter the index pattern `parcs-*` in the **Index pattern** field. This will match all indices that begin with `parcs-`, including logs related to the PARCS system.
   - Click **"Next step"** to configure the data view.

3. **Save the Data View**:
   - Click **"Create data view"** to save the configuration.

4. **Explore the Logs**:
   - Once the data view is created, you can use Kibana’s **Discover** tab to explore the PARCS logs.
   - You can filter logs by various fields such as log level, service (e.g., `parcs-daemon` or `parcs-hostapi`), and timestamp.
   - Use Kibana’s search and filtering capabilities to drill down into specific logs or view trends over time.


## Contributing
Contributions to the PARCS-NET-K8 solution are welcome! If you encounter any issues or have suggestions for improvements, please open an issue or submit a pull request in this repository.

## License
This repository is licensed under the [MIT License](LICENSE). Feel free to use and modify the code as per the terms of the license.

## Acknowledgements
We would like to thank the contributors and maintainers of the PARCS-NET-K8 solution. Your efforts and support are greatly appreciated.
