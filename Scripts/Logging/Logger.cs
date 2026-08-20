namespace LiteNetLibManager
{
    public class Logger<T> : ILogger<T>, ILogger
    {
        private readonly ILogger _logger;

        public Logger(ILoggerFactory factory)
        {
            if (factory == null)
            {
                throw new System.ArgumentNullException("factory");
            }

            _logger = factory.CreateLogger(TypeNameUtils.GetTypeDisplayName(typeof(T), fullName: true, includeGenericParameterNames: false, includeGenericParameters: false, '.'));
        }

        public void LogInformation(string message, params object[] args)
        {
            _logger.LogInformation(message, args);
        }

        public void LogError(string message, params object[] args)
        {
            _logger.LogError(message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            _logger.LogWarning(message, args);
        }
    }
}