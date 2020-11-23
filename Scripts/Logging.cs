using Cysharp.Threading.Tasks;
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
            InvokeOnLog(LogType.Log, tag, message.ToString()).Forget();
        }

        public static void LogError(string tag, object message)
        {
            if (message == null)
                return;
            InvokeOnLog(LogType.Error, tag, message.ToString()).Forget();
        }

        public static void LogWarning(string tag, object message)
        {
            if (message == null)
                return;
            InvokeOnLog(LogType.Warning, tag, message.ToString()).Forget();
        }

        public static void LogException(string tag, System.Exception ex)
        {
            if (ex == null)
                return;
            InvokeOnLog(LogType.Exception, tag, ex.ToString()).Forget();
        }

        public static void Log(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(LogType.Log, NoTag, message.ToString()).Forget();
        }

        public static void LogError(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(LogType.Error, NoTag, message.ToString()).Forget();
        }

        public static void LogWarning(object message)
        {
            if (message == null)
                return;
            InvokeOnLog(LogType.Warning, NoTag, message.ToString()).Forget();
        }

        public static void LogException(System.Exception ex)
        {
            if (ex == null)
                return;
            InvokeOnLog(LogType.Exception, NoTag, ex.ToString()).Forget();
        }

        private static async UniTaskVoid InvokeOnLog(LogType logType, string tag, string text)
        {
            await UniTask.SwitchToMainThread();
#if UNITY_EDITOR
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(text);
                    break;
                case LogType.Error:
                case LogType.Exception:
                    Debug.LogError(text);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(text);
                    break;
            }
#endif
            if (onLog != null)
                onLog.Invoke(logType, tag, text);
        }
    }
}
