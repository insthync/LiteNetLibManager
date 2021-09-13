using Microsoft.Extensions.Logging;
using UnityEngine;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static void Log(string tag, object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.Log($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogInformation(message.ToString());
        }

        public static void LogError(string tag, object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogError($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogError(message.ToString());
        }

        public static void LogWarning(string tag, object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogWarning($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogWarning(message.ToString());
        }

        public static void LogException(string tag, System.Exception ex)
        {
            if (ex == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogError($"[{tag}] {ex}");
                return;
            }
            LogManager.GetLogger(tag).LogError(ex.ToString());
        }

        public static void Log(object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.Log($"{message}");
                return;
            }
            LogManager.Logger.LogInformation(message.ToString());
        }

        public static void LogError(object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogError($"{message}");
                return;
            }
            LogManager.Logger.LogError(message.ToString());
        }

        public static void LogWarning(object message)
        {
            if (message == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogWarning($"{message}");
                return;
            }
            LogManager.Logger.LogWarning(message.ToString());
        }

        public static void LogException(System.Exception ex)
        {
            if (ex == null)
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                Debug.LogError($"{ex}");
                return;
            }
            LogManager.Logger.LogError(ex.ToString());
        }
    }
}
