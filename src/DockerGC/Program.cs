namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using StatsdClient;
    using System.Threading;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Net.Http;
    using System.Threading.Tasks;

    public enum ImageDeletionOrderType 
    { 
        ByImageCreationDate,
        ByImageLastTouchDate 
    }

    public enum RecyclingStrategyType
    { 
        ByDate,
        ByDiskSpace
    }

    class Program
    {
        static void ExitWithError(string error)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc exits with error: {error}");
            Environment.Exit(1);
        }

        static void ValidateEnvironmentVariable(string envVariable) 
        {
            if (Environment.GetEnvironmentVariable(envVariable) == null) 
            {
                ExitWithError($"Environment variable not set: {envVariable}");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc started");
            
            ILogger logger;

            var statsdEnabled = bool.Parse(Environment.GetEnvironmentVariable("DOCKERGC_STATSD_LOGGER_ENABLED") ?? "False");
            var statsdHost = Environment.GetEnvironmentVariable("DOCKERGC_STATSD_HOST") ?? "localhost";
            var statsdPort = Environment.GetEnvironmentVariable("DOCKERGC_STATSD_PORT") ?? "8125";

            if (statsdEnabled) 
            {
                Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Configured to send counters to statsd. Host: {statsdHost} Port: {statsdPort}");
                logger = new StatsdLogger(statsdHost, statsdPort);
            } 
            else
            {
                logger = new ConsoleLogger();
            }

            ValidateEnvironmentVariable("DOCKERGC_RECYCLING_STRATEGY");
            ValidateEnvironmentVariable("DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES");
            ValidateEnvironmentVariable("DOCKERGC_IMAGE_DELETION_ORDER");

            int executionIntervalInMinutes;
            if (int.TryParse(Environment.GetEnvironmentVariable("DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES")?.ToString(), out executionIntervalInMinutes))
            {
                if (executionIntervalInMinutes != -1 && executionIntervalInMinutes < 0)
                {
                    ExitWithError($"DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES should be greater than zero or set to -1 for one time execution");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc is configured to run every {executionIntervalInMinutes} minutes");
                }
            }
            else
            {
                ExitWithError($"DOCKERGC_EXECUTION_INTERVAL_IN_MINUTES should be an integer");
            }     

            RecyclingStrategy strategy = null;
            var recyclingStrategyEnv = Environment.GetEnvironmentVariable("DOCKERGC_RECYCLING_STRATEGY")?.ToString();
            var recyclingStrategy = (RecyclingStrategyType)RecyclingStrategyType.Parse(typeof(RecyclingStrategyType), recyclingStrategyEnv);
            
            if (!Enum.IsDefined(typeof(RecyclingStrategyType), recyclingStrategy))
            {
                ExitWithError($"Invalid DOCKERGC_RECYCLING_STRATEGY: {recyclingStrategyEnv}. Valid recycling strategies are: {string.Join(", ", Enum.GetNames(typeof(RecyclingStrategyType)))}");
            }

            var imageDeletionOrderEnv = Environment.GetEnvironmentVariable("DOCKERGC_IMAGE_DELETION_ORDER")?.ToString();
            var imageDeletionOrder = (ImageDeletionOrderType)RecyclingStrategyType.Parse(typeof(ImageDeletionOrderType), imageDeletionOrderEnv);
            
            if (!Enum.IsDefined(typeof(ImageDeletionOrderType), imageDeletionOrder))
            {
                ExitWithError($"Invalid DOCKERGC_IMAGE_DELETION_ORDER: {imageDeletionOrder}. Valid image deletion orders are: {string.Join(", ", Enum.GetNames(typeof(ImageDeletionOrderType)))}");
            }

            var containerStateBlacklist = new Matchlist(Environment.GetEnvironmentVariable("DOCKERGC_CONTAINER_STATE_BLACKLIST"));
            var imageWhitelist = new Matchlist(Environment.GetEnvironmentVariable("DOCKERGC_IMAGE_WHITELIST"));
            
            var imageLastTouchDate = new ConcurrentDictionary<string, DateTime>();
            GC.KeepAlive(imageLastTouchDate);

            if (recyclingStrategy is RecyclingStrategyType.ByDate)
            {
                ValidateEnvironmentVariable("DOCKERGC_DAYS_BEFORE_DELETION");

                int daysBeforeDeletion;
                if (int.TryParse(Environment.GetEnvironmentVariable("DOCKERGC_DAYS_BEFORE_DELETION")?.ToString(), out daysBeforeDeletion))
                {
                    if (daysBeforeDeletion > 0)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Reclying strategy is set to [ {recyclingStrategy} | {daysBeforeDeletion} days | {imageDeletionOrder} ]");
                        strategy = new ByDateRecyclingStrategy(daysBeforeDeletion, imageWhitelist, containerStateBlacklist, imageLastTouchDate, imageDeletionOrder);
                    }
                    else
                    {
                        ExitWithError("DOCKERGC_DAYS_BEFORE_DELETION should be greater than zero");
                    }
                }
                else
                {
                    ExitWithError("DOCKERGC_DAYS_BEFORE_DELETION should be an integer");
                }
            }
            else if (recyclingStrategy is RecyclingStrategyType.ByDiskSpace)
            {
                ValidateEnvironmentVariable("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE");

                double sizeLimitInGigabyte;
                if (double.TryParse(Environment.GetEnvironmentVariable("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE")?.ToString(), out sizeLimitInGigabyte))
                {
                    if (sizeLimitInGigabyte >= 0)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Reclying strategy is set to [ {recyclingStrategy} | {sizeLimitInGigabyte} GB | {imageDeletionOrder} ]");
                        strategy = new ByDiskSpaceRecyclingStrategy(sizeLimitInGigabyte, imageWhitelist, containerStateBlacklist, imageLastTouchDate, imageDeletionOrder);
                    }
                    else
                    {
                        ExitWithError("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE should be greater than or equal to zero");
                    }
                }
                else
                {
                    ExitWithError("DOCKERGC_SIZE_LIMIT_IN_GIGABYTE should be a double or an integer");
                }
            }
            
            var dockerEndpoint = Environment.GetEnvironmentVariable("DOCKERGC_DOCKER_ENDPOINT")?.ToString();
            if (string.IsNullOrEmpty(dockerEndpoint))
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    dockerEndpoint = "tcp://localhost:2375"; // default docker host on Windows
                }
                else // Linux or Mac OSX
                {
                    dockerEndpoint = "unix:///var/run/docker.sock"; // default docker host on Linux/OSX
                }
            }

            var defaultTimeoutInSecondsStr = Environment.GetEnvironmentVariable("DOCKERGC_DOCKER_CLIENT_TIMEOUT_IN_SECONDS")?.ToString();

            // Set default timeout to be 100 second
            var defaultTimeout = TimeSpan.FromSeconds(100);
            if (!string.IsNullOrEmpty(defaultTimeoutInSecondsStr))
            {
                int timeout;
                if (int.TryParse(defaultTimeoutInSecondsStr, out timeout))
                {
                    if (timeout > 0) 
                    {
                        defaultTimeout = TimeSpan.FromSeconds(int.Parse(defaultTimeoutInSecondsStr));
                    }
                    else 
                    {
                        ExitWithError("DOCKERGC_DOCKER_CLIENT_TIMEOUT_IN_SECONDS should be greater than zero");
                    }
                } 
                else
                {
                    ExitWithError("DOCKERGC_DOCKER_CLIENT_TIMEOUT_IN_SECONDS should be an integer");
                }
            }
            
            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Docker daemon endpoint is set to: {dockerEndpoint}");
            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] Docker client timeout is set to: {defaultTimeout.TotalSeconds} seconds");
            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] docker-gc event listener retry backoff is set to: {defaultTimeout.TotalSeconds} seconds");
            
            try 
            {
                var dockerClient = new DockerClientConfiguration(new Uri(dockerEndpoint), defaultTimeout: defaultTimeout).CreateClient();

                // Ping docker daemon to see if endpoint is valid, throw exception if not
                dockerClient.System.PingAsync().Wait();

                CancellationTokenSource cancellation = new CancellationTokenSource();

                if (imageDeletionOrder is ImageDeletionOrderType.ByImageLastTouchDate)
                {
                    // Start monitoring container events and keep track of image last touch date
                    // Task will finish or throw exception if docker daemon went offline or connection get lost
                    // We are using retry logic here to make sure we reconnect to daemon and resume listening on container events when that happens
                    ExecuteLongRunningTaskWithRetry(async () => await MonitorContainerEventsAsync(imageLastTouchDate, dockerClient, cancellation), 
                        defaultTimeout,
                        $"docker-gc failed to monitor container events, retry in {defaultTimeout.TotalSeconds} seconds"
                        );
                }

                var dockerGC = new DockerGarbageCollector(dockerClient,
                                                          new DockerImageDependencyTreeBuilder(), 
                                                          new DockerRepositoryDescriptor(),
                                                          strategy,
                                                          containerStateBlacklist,
                                                          executionIntervalInMinutes,
                                                          logger);
                dockerGC.Run();
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    if (ex is HttpRequestException) 
                    {
                        ExitWithError("docker-gc failed to establish connection with docker daemon: " + ((HttpRequestException)ex).GetBaseException().Message);
                    }
                }
                ExitWithError(ae.ToString());
            }
            catch (Exception e)
            {
                ExitWithError(e.ToString());
            }
        }

        private static async Task MonitorContainerEventsAsync(ConcurrentDictionary<string, DateTime> imageLastTouchDate, DockerClient dockerClient, CancellationTokenSource cancellation)
        {
            await dockerClient.System.MonitorEventsAsync(
                    new ContainerEventsParameters(),
                    new EventProgress()
                    {
                        _onCalled = (m) =>
                        {
                            try
                            {
                                // m.From is the image name (tag name)
                                // m.From can be a short ID of the image when tag is <none>, example: 2987f2fe8c12
                                if (!string.IsNullOrEmpty(m.From))
                                {
                                    // Docker event message does not contain image ID so we need to query docker deamon
                                    // to get image ID for accuracy
                                    var imageId = dockerClient.Images.InspectImageAsync(m.From).Result.ID;
                                    imageLastTouchDate[imageId] = m.Time;
                                }
                            }
                            catch (Exception e) // In case docker inspect throws exception (most probably race condition: image get removed after receiving the event) 
                            {
                                Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {e}");
                            }
                        }
                    }, cancellation.Token);
        }

        private static async void ExecuteLongRunningTaskWithRetry(Func<Task> task, TimeSpan backoff, string errorMsg)
        {
            while (true)
            {
                try
                {
                    await task();
                    Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {errorMsg}");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {errorMsg} ({e.GetType().Name} : {e.Message})");
                }

                Thread.Sleep(backoff);
            }
        }
    }
}
