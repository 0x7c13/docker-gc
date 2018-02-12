namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using StatsdClient;

    class Program
    {
        static void ValidateEnvironmentVariable(string envVariable) 
        {
            if (Environment.GetEnvironmentVariable(envVariable) == null) 
            {
                //throw new Exception($"Environment variable not set: {envVariable}");
                Console.Error.WriteLine($"Environment variable not set: {envVariable}");
                Environment.Exit(1);
            }
        }

        static void Main(string[] args)
        {
            ILogger logger;

            var statsdEnabled = bool.Parse(Environment.GetEnvironmentVariable("STATSD_LOGGER_ENABLED") ?? "False");
            var statsdHost = Environment.GetEnvironmentVariable("STATSD_HOST") ?? "localhost";
            var statsdPort = Environment.GetEnvironmentVariable("STATSD_PORT") ?? "8125";

            if (statsdEnabled) 
            {
                logger = new StatsdLogger(statsdHost, statsdPort);
            } 
            else
            {
                logger = new ConsoleLogger();
            }

            ValidateEnvironmentVariable("DOCKERGC_RECYCLING_STRATEGY");
            ValidateEnvironmentVariable("DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES");

            RecyclingStrategy recyclingStrategy;

            var recyclingStrategytype = Environment.GetEnvironmentVariable("DOCKERGC_RECYCLING_STRATEGY")?.ToString();
            var executionIntervalInMinutes = int.Parse(Environment.GetEnvironmentVariable("DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES")?.ToString());

            var containerStateBlacklist = new Matchlist(Environment.GetEnvironmentVariable("DOCKERGC_CONTAINER_STATE_BLACKLIST"));
            var imageWhitelist = new Matchlist(Environment.GetEnvironmentVariable("DOCKERGC_IMAGE_WHITELIST"));
            
            ValidateEnvironmentVariable("DOCKERGC_WAIT_FOR_CONTAINERS_IN_BLACKLIST_STATE_IN_DAYS");
            var waitToleranceOfBlacklistStateContainersInDays = int.Parse(Environment.GetEnvironmentVariable("DOCKERGC_WAIT_FOR_CONTAINERS_IN_BLACKLIST_STATE_IN_DAYS")?.ToString());

            if (string.Equals(recyclingStrategytype, ByDateRecyclingStrategy.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                ValidateEnvironmentVariable("DOCKERGC_DAYS_BEFORE_DELETION");
                var daysBeforeDeletion = int.Parse(Environment.GetEnvironmentVariable("DOCKERGC_DAYS_BEFORE_DELETION")?.ToString());
                recyclingStrategy = new ByDateRecyclingStrategy(daysBeforeDeletion, imageWhitelist, containerStateBlacklist, waitToleranceOfBlacklistStateContainersInDays);
            } 
            else if (string.Equals(recyclingStrategytype, ByDiskSpaceRecyclingStrategy.Name, StringComparison.InvariantCultureIgnoreCase)) 
            {
                ValidateEnvironmentVariable("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE");
                var sizeLimitInGigabyte = double.Parse(Environment.GetEnvironmentVariable("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE")?.ToString());
                recyclingStrategy = new ByDiskSpaceRecyclingStrategy(sizeLimitInGigabyte, imageWhitelist, containerStateBlacklist, waitToleranceOfBlacklistStateContainersInDays);
            }
            else 
            {
                throw new Exception("Invalid environment variable value for: DOCKERGC_RECYCLING_STRATEGY");
            }
            
            ValidateEnvironmentVariable("DOCKERGC_DOCKER_ENDPOINT");
            var dockerEndpoint = Environment.GetEnvironmentVariable("DOCKERGC_DOCKER_ENDPOINT")?.ToString();

            try 
            {
                var dockerClient = new DockerClientConfiguration(new Uri(dockerEndpoint)).CreateClient();

                // call network api to see if docker deamon is connected, will throw exception if not
                var networks = dockerClient.Networks.ListNetworksAsync().Result;

                var dockerGC = new DockerGarbageCollector(dockerClient,
                                                          new DockerImageDependencyTreeBuilder(), 
                                                          new DockerImageDependencyTreePrinter(2, "<--- TBD"),
                                                          recyclingStrategy,
                                                          containerStateBlacklist,
                                                          executionIntervalInMinutes,
                                                          logger);
                dockerGC.Run();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Environment.Exit(1);
            }
        }
    }
}
