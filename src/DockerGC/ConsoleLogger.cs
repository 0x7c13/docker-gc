namespace DockerGC
{
    using System;

    internal class ConsoleLogger : ILogger
    {
        public void LogCounter(string key, int value)
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString()} UTC] {key}: {value}");
        }
    }
}