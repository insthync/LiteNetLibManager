namespace LiteNetLibManager
{
    public enum ELogLevel : byte
    {
        Developer = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
    }

    public static class LogLevelExtensions
    {
        public static bool IsLogDev(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Developer;
        }

        public static bool IsLogDebug(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Debug;
        }

        public static bool IsLogInfo(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Info;
        }

        public static bool IsLogWarn(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Warn;
        }

        public static bool IsLogError(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Error;
        }

        public static bool IsLogFatal(this ELogLevel currentLogLevel)
        {
            return currentLogLevel <= ELogLevel.Fatal;
        }
    }
}
