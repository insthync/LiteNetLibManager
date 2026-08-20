namespace LiteNetLibManager
{
    public interface ILoggerFactory : System.IDisposable
    {
        ILogger CreateLogger(string categoryName);
    }
}
