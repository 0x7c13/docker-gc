namespace DockerGC
{
    public interface ILogger 
    {
        void LogCounter(string key, int value);
    }
}