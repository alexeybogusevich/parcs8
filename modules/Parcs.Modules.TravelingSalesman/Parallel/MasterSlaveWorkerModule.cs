using Microsoft.Extensions.Logging;
using Parcs.Net;
using Parcs.Modules.TravelingSalesman.Models;

namespace Parcs.Modules.TravelingSalesman.Parallel
{
    /// <summary>
    /// Worker module for Master-Slave GA.
    /// Workers only calculate fitness (distance) for routes sent by master.
    /// This is the parallelized part that gives real speedup.
    /// </summary>
    public class MasterSlaveWorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            moduleInfo.Logger.LogInformation("Master-Slave Worker module started - ready for fitness evaluation");
            
            try
            {
                // Receive cities data (needed for distance calculation) - binary format
                var cities = await ReadCitiesBinaryAsync(moduleInfo.Parent);
                moduleInfo.Logger.LogInformation("Worker received {CitiesCount} cities for distance calculation", cities.Count);

                // Worker loop: continuously receive routes, calculate fitness, send back
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Receive batch of routes in binary format (much faster than JSON)
                        var routes = await ReadRoutesBinaryAsync(moduleInfo.Parent);
                        
                        if (routes == null || routes.Count == 0)
                        {
                            moduleInfo.Logger.LogInformation("Received empty batch, worker terminating");
                            break;
                        }

                        moduleInfo.Logger.LogInformation("Worker received {RoutesCount} routes for fitness evaluation", routes.Count);

                        // OPTIMIZATION: Use arrays and avoid allocations in hot path
                        var fitnessValues = new List<double>(routes.Count);
                        foreach (var routeCities in routes)
                        {
                            double totalDistance = 0;
                            int routeLength = routeCities.Count;
                            
                            // Pre-calculate modulo once
                            for (int i = 0; i < routeLength; i++)
                            {
                                int currentCityIndex = routeCities[i];
                                int nextCityIndex = routeCities[(i + 1) % routeLength];
                                
                                // OPTIMIZATION: Direct array access instead of indexer
                                totalDistance += cities[currentCityIndex].DistanceTo(cities[nextCityIndex]);
                            }
                            fitnessValues.Add(totalDistance);
                        }

                        moduleInfo.Logger.LogInformation("Worker calculated fitness for {RoutesCount} routes", routes.Count);

                        // Send fitness values back as binary data (faster than JSON)
                        await WriteFitnessValuesBinaryAsync(moduleInfo.Parent, fitnessValues);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        moduleInfo.Logger.LogError(ex, "Error processing fitness evaluation batch: {Message}", ex.Message);
                        break;
                    }
                }

                moduleInfo.Logger.LogInformation("Master-Slave Worker module completed");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                moduleInfo.Logger.LogError(ex, "Master-Slave Worker module error: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Read cities from binary format.
        /// Format: [numCities (int)][city1Id (int)][city1X (double)][city1Y (double)]...
        /// </summary>
        private static async Task<List<City>> ReadCitiesBinaryAsync(IChannel channel)
        {
            var bytes = await channel.ReadBytesAsync();
            using var ms = new MemoryStream(bytes);
            var reader = new BinaryReader(ms);
            
            int count = reader.ReadInt32();
            var cities = new List<City>(count);
            
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                double x = reader.ReadDouble();
                double y = reader.ReadDouble();
                cities.Add(new City(id, x, y));
            }
            
            return cities;
        }

        /// <summary>
        /// Read routes from binary format.
        /// Format: [numRoutes (int)][route1Length (int)][route1Data (int[])][route2Length (int)][route2Data (int[])]...
        /// </summary>
        private static async Task<List<List<int>>> ReadRoutesBinaryAsync(IChannel channel)
        {
            var bytes = await channel.ReadBytesAsync();
            using var ms = new MemoryStream(bytes);
            var reader = new BinaryReader(ms);
            
            int numRoutes = reader.ReadInt32();
            var routes = new List<List<int>>(numRoutes);
            
            for (int i = 0; i < numRoutes; i++)
            {
                int routeLength = reader.ReadInt32();
                var route = new List<int>(routeLength);
                
                for (int j = 0; j < routeLength; j++)
                {
                    route.Add(reader.ReadInt32());
                }
                
                routes.Add(route);
            }
            
            return routes;
        }

        /// <summary>
        /// Write fitness values as binary data.
        /// Format: [numValues (int)][value1 (double)][value2 (double)]...
        /// </summary>
        private static async ValueTask WriteFitnessValuesBinaryAsync(IChannel channel, List<double> fitnessValues)
        {
            using var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            
            writer.Write(fitnessValues.Count);
            foreach (var value in fitnessValues)
            {
                writer.Write(value);
            }
            
            await channel.WriteDataAsync(ms.ToArray());
        }
    }
}

