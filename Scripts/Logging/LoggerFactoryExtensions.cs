namespace LiteNetLibManager
{
    public static class LoggerFactoryExtensions
    {
        public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory)
        {
            if (factory == null)
            {
                throw new System.ArgumentNullException("factory");
            }

            return new Logger<T>(factory);
        }

        public static ILogger CreateLogger(this ILoggerFactory factory, System.Type type)
        {
            if (factory == null)
            {
                throw new System.ArgumentNullException("factory");
            }

            if (type == null)
            {
                throw new System.ArgumentNullException("type");
            }

            return factory.CreateLogger(TypeNameUtils.GetTypeDisplayName(type, fullName: true, includeGenericParameterNames: false, includeGenericParameters: false, '.'));
        }
    }
}