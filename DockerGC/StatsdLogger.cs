namespace DockerGC
{
    using StatsdClient;

    internal class StatsdLogger : ILogger
    {
        public StatsdLogger(string statsdHost, string statsdPort)
        {
            Metrics.Configure(new MetricsConfig
                {
                    StatsdServerName = statsdHost,
                    StatsdServerPort = int.Parse(statsdPort),
                    Prefix = "docker-gc"
                });
        }

        public void LogCounter(string key, int value)
        {
            Metrics.Counter(key, value);
        }
    }
}