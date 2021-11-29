using Cysharp.Text;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Generic;
using ZLogger;

namespace LiteNetLibManager
{
    public static partial class LogManager
    {
        static ILogger noTagLogger;
        static ILoggerFactory loggerFactory;
        public static ILoggerFactory LoggerFactory
        {
            get => loggerFactory;
            set
            {
                if (loggerFactory != null)
                    loggerFactory.Dispose();
                LoggerByTypes.Clear();
                LoggerByTags.Clear();
                loggerFactory = value;
                noTagLogger = loggerFactory.CreateLogger("No Tag");
            }
        }
        static readonly Dictionary<string, ILogger> LoggerByTypes = new Dictionary<string, ILogger>();
        static readonly Dictionary<string, ILogger> LoggerByTags = new Dictionary<string, ILogger>();

        public static bool IsLoggerFactoryDisposed { get; private set; } = false;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // Standard LoggerFactory does not work on IL2CPP,
            // But you can use ZLogger's UnityLoggerFactory instead,
            // it works on IL2CPP, all platforms(includes mobile).
            LoggerFactory = UnityLoggerFactory.Create(builder =>
            {
                // or more configuration, you can use builder.AddFilter
                builder.SetMinimumLevel(LogLevel.Trace);

                // AddZLoggerUnityDebug is only available for Unity, it send log to UnityEngine.Debug.Log.
                // LogLevels are translate to
                // * Trace/Debug/Information -> LogType.Log
                // * Warning/Critical -> LogType.Warning
                // * Error without Exception -> LogType.Error
                // * Error with Exception -> LogException
                builder.AddZLoggerUnityDebug();
            });

            Logger.LogInformation("===== Logger Initialized =====");

            UnityEngine.Application.quitting += () =>
            {
                // when quit, flush unfinished log entries.
                if (loggerFactory != null)
                    loggerFactory.Dispose();
                loggerFactory = null;
                IsLoggerFactoryDisposed = true;
            };
        }

        public static void PrefixFormatterConfigure(IBufferWriter<byte> writer, LogInfo info)
        {
            switch (info.LogLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    ZString.Utf8Format(writer, " INFO {0} [{1}] - ", info.CategoryName, info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
                case LogLevel.Warning:
                case LogLevel.Critical:
                    ZString.Utf8Format(writer, " WARN {0} [{1}] - ", info.CategoryName, info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
                case LogLevel.Error:
                    ZString.Utf8Format(writer, "ERROR {0} [{1}] - ", info.CategoryName, info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
            }
        }

        public static ILogger Logger => noTagLogger;

        public static ILogger<T> GetLogger<T>() where T : class
        {
            string typeFullName = typeof(T).FullName;
            if (!LoggerByTypes.ContainsKey(typeFullName))
                LoggerByTypes.Add(typeFullName, LoggerFactory.CreateLogger<T>());
            return LoggerByTypes[typeFullName] as ILogger<T>;
        }

        public static ILogger GetLogger(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return Logger;
            if (!LoggerByTags.ContainsKey(tag))
                LoggerByTags.Add(tag, LoggerFactory.CreateLogger(tag));
            return LoggerByTags[tag];
        }
    }
}
