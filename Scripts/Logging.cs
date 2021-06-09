using Microsoft.Extensions.Logging;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static void Log(string tag, object message)
        {
            if (message == null)
                return;
            LogManager.GetLogger(tag).LogInformation(message.ToString());
        }

        public static void LogError(string tag, object message)
        {
            if (message == null)
                return;
            LogManager.GetLogger(tag).LogError(message.ToString());
        }

        public static void LogWarning(string tag, object message)
        {
            if (message == null)
                return;
            LogManager.GetLogger(tag).LogWarning(message.ToString());
        }

        public static void LogException(string tag, System.Exception ex)
        {
            if (ex == null)
                return;
            LogManager.GetLogger(tag).LogError(ex.ToString());
        }

        public static void Log(object message)
        {
            if (message == null)
                return;
            LogManager.Logger.LogInformation(message.ToString());
        }

        public static void LogError(object message)
        {
            if (message == null)
                return;
            LogManager.Logger.LogError(message.ToString());
        }

        public static void LogWarning(object message)
        {
            if (message == null)
                return;
            LogManager.Logger.LogWarning(message.ToString());
        }

        public static void LogException(System.Exception ex)
        {
            if (ex == null)
                return;
            LogManager.Logger.LogError(ex.ToString());
        }
    }
}
