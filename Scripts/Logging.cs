using Microsoft.Extensions.Logging;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public const string NoTag = "(No Tag)";

        public static void Log(string tag, object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Log, tag, message.ToString());
        }

        public static void LogError(string tag, object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Error, tag, message.ToString());
        }

        public static void LogWarning(string tag, object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Warning, tag, message.ToString());
        }

        public static void LogException(string tag, System.Exception ex)
        {
            if (ex == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Exception, tag, ex.ToString());
        }

        public static void Log(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Log, NoTag, message.ToString());
        }

        public static void LogError(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Error, NoTag, message.ToString());
        }

        public static void LogWarning(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Warning, NoTag, message.ToString());
        }

        public static void LogException(System.Exception ex)
        {
            if (ex == null)
                return;
            InvokeOnLog(UnityEngine.LogType.Exception, NoTag, ex.ToString());
        }

        private static void InvokeOnLog(UnityEngine.LogType logType, string tag, string text)
        {
            switch (logType)
            {
                case UnityEngine.LogType.Log:
                    LogManager.GetLogger(tag).LogInformation(text);
                    break;
                case UnityEngine.LogType.Error:
                case UnityEngine.LogType.Exception:
                    LogManager.GetLogger(tag).LogError(text);
                    break;
                case UnityEngine.LogType.Warning:
                    LogManager.GetLogger(tag).LogWarning(text);
                    break;
            }
        }
    }
}
