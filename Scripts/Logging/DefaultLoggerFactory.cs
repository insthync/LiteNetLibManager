namespace LiteNetLibManager
{
    public class DefaultLoggerFactory : ILoggerFactory
    {
        private readonly string _logFilePathPrefix;
        public DefaultLoggerFactory(string logFilePathPrefix)
        {
            _logFilePathPrefix = logFilePathPrefix;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new DefaultLogger(categoryName, _logFilePathPrefix);
        }

        public void Dispose()
        {

        }
    }
}
