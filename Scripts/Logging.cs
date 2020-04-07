using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static System.Action<LogType, string, string> onLog;
        public const string NoTag = "No Tag";

        public static void Log(string tag, object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.Log("[" + tag + "] " + message.ToString());
#endif
            onLog.Invoke(LogType.Log, tag, message.ToString());
        }

        public static void LogError(string tag, object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.LogError("[" + tag + "] " + message.ToString());
#endif
            onLog.Invoke(LogType.Log, tag, message.ToString());
        }

        public static void LogWarning(string tag, object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.LogWarning("[" + tag + "] " + message.ToString());
#endif
            onLog.Invoke(LogType.Warning, tag, message.ToString());
        }

        public static void LogException(string tag, System.Exception ex)
        {
            if (ex == null)
                return;
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
            onLog.Invoke(LogType.Exception, tag, ex.ToString());
        }

        public static void Log(object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.Log(message);
#endif
            onLog.Invoke(LogType.Log, NoTag, message.ToString());
        }

        public static void LogError(object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.LogError(message);
#endif
            onLog.Invoke(LogType.Error, NoTag, message.ToString());
        }

        public static void LogWarning(object message)
        {
            if (message == null)
                return;
#if UNITY_EDITOR
            Debug.LogWarning(message);
#endif
            onLog.Invoke(LogType.Warning, NoTag, message.ToString());
        }

        public static void LogException(System.Exception ex)
        {
            if (ex == null)
                return;
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
            onLog.Invoke(LogType.Exception, NoTag, ex.ToString());
        }
    }
}
