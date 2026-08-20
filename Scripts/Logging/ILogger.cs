namespace LiteNetLibManager
{
    public interface ILogger
    {
        void LogInformation(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogWarning(string message, params object[] args);
    }

    public interface ILogger<out TCategoryName> : ILogger
    {
    }
}
