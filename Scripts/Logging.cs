using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public static class Logging
    {
        public static void Log(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.Log("[" + tag + "] " + message.ToString());
#endif
        }

        public static void LogError(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.LogError("[" + tag + "] " + message.ToString());
#endif
        }

        public static void LogWarning(string tag, object message)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[" + tag + "] " + message.ToString());
#endif
        }

        public static void LogException(string tag, System.Exception ex)
        {
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
        }

        public static void Log(object message)
        {
#if UNITY_EDITOR
            Debug.Log(message);
#endif
        }

        public static void LogError(object message)
        {
#if UNITY_EDITOR
            Debug.LogError(message);
#endif
        }

        public static void LogWarning(object message)
        {
#if UNITY_EDITOR
            Debug.LogWarning(message);
#endif
        }

        public static void LogException(System.Exception ex)
        {
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
        }
    }
}
