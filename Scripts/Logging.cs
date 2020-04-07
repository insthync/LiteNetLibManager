using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static System.Action<string, object> onLog;
        public static System.Action<string, object> onLogError;
        public static System.Action<string, object> onLogWarning;
        public static System.Action<string, System.Exception> onLogException;


        public static void Log(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.Log("[" + tag + "] " + message.ToString());
#endif
            onLog.Invoke(tag, message);
        }

        public static void LogError(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.LogError("[" + tag + "] " + message.ToString());
#endif
            onLogError.Invoke(tag, message);
        }

        public static void LogWarning(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[" + tag + "] " + message.ToString());
#endif
            onLogWarning.Invoke(tag, message);
        }

        public static void LogException(string tag, System.Exception ex)
        {
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
            onLogException.Invoke(tag, ex);
        }

        public static void Log(object message)
        {
#if UNITY_EDITOR
            Debug.Log(message);
#endif
            onLog.Invoke(string.Empty, message);
        }

        public static void LogError(object message)
        {
#if UNITY_EDITOR
            Debug.LogError(message);
#endif
            onLogError.Invoke(string.Empty, message);
        }

        public static void LogWarning(object message)
        {
#if UNITY_EDITOR
            Debug.LogWarning(message);
#endif
            onLogWarning.Invoke(string.Empty, message);
        }

        public static void LogException(System.Exception ex)
        {
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
            onLogException.Invoke(string.Empty, ex);
        }
    }
}
