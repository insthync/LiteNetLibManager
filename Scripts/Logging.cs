using Microsoft.Extensions.Logging;
using UnityEngine;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static void Log(string tag, object message)
        {
            Log(tag, message.ToString());
        }

        public static void Log(string tag, string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.Log(string.Format($"[{tag}] {message}", args));
                else
                    Debug.Log($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogInformation(message, args);
        }

        public static void LogError(string tag, object message)
        {
            LogError(tag, message.ToString());
        }

        public static void LogError(string tag, string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.LogError(string.Format($"[{tag}] {message}", args));
                else
                    Debug.LogError($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogError(message, args);
        }

        public static void LogWarning(string tag, object message)
        {
            LogWarning(tag, message.ToString());
        }

        public static void LogWarning(string tag, string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.LogWarning(string.Format($"[{tag}] {message}", args));
                else
                    Debug.LogWarning($"[{tag}] {message}");
                return;
            }
            LogManager.GetLogger(tag).LogWarning(message, args);
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
            Log(message.ToString());
        }

        public static void Log(string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.Log(string.Format($"{message}", args));
                else
                    Debug.Log($"{message}");
                return;
            }
            LogManager.Logger.LogInformation(message, args);
        }

        public static void LogError(object message)
        {
            LogError(message.ToString());
        }

        public static void LogError(string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.LogError(string.Format($"{message}", args));
                else
                    Debug.LogError($"{message}");
                return;
            }
            LogManager.Logger.LogError(message, args);
        }

        public static void LogWarning(object message)
        {
            LogWarning(message.ToString());
        }

        public static void LogWarning(string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (LogManager.IsLoggerFactoryDisposed)
            {
                if (args.Length > 0)
                    Debug.LogWarning(string.Format($"{message}", args));
                else
                    Debug.LogWarning($"{message}");
                return;
            }
            LogManager.Logger.LogWarning(message, args);
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
