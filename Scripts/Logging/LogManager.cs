using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LoggerManager
    {
        ILogger _defaultLogger;
        ILoggerFactory _loggerFactory;
        public ILoggerFactory LoggerFactory
        {
            get => _loggerFactory;
        }

        public ILogger Logger => _defaultLogger;

        public bool IsDisposed { get; private set; } = false;

        readonly Dictionary<string, ILogger> _loggerByTypes = new Dictionary<string, ILogger>();
        readonly Dictionary<string, ILogger> _loggerByTags = new Dictionary<string, ILogger>();

        public LoggerManager(ILoggerFactory loggerFactory)
        {
            _loggerByTypes.Clear();
            _loggerByTags.Clear();
            _loggerFactory = loggerFactory;
            _defaultLogger = loggerFactory.CreateLogger("N/A");

            UnityEngine.Application.quitting += () =>
            {
                // when quit, flush unfinished log entries.
                if (_loggerFactory != null)
                    _loggerFactory.Dispose();
                _loggerFactory = null;
                IsDisposed = true;
            };
        }

        public ILogger<T> GetLogger<T>() where T : class
        {
            string typeFullName = typeof(T).FullName;
            if (!_loggerByTypes.ContainsKey(typeFullName))
                _loggerByTypes.Add(typeFullName, LoggerFactory.CreateLogger<T>());
            return _loggerByTypes[typeFullName] as ILogger<T>;
        }

        public ILogger GetLogger(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return Logger;
            if (!_loggerByTags.ContainsKey(tag))
                _loggerByTags.Add(tag, LoggerFactory.CreateLogger(tag));
            return _loggerByTags[tag];
        }
    }

    public static partial class LogManager
    {
        public static LoggerManager LoggerManager { get; set; }


        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            LoggerManager = new LoggerManager(new DefaultLoggerFactory(null));
            LoggerManager.Logger.LogInformation("===== Logger Initialized =====");
        }

        public static bool IsLoggerDisposed => LoggerManager.IsDisposed;

        public static ILogger Logger => LoggerManager.Logger;

        public static ILogger<T> GetLogger<T>() where T : class
        {
            return LoggerManager.GetLogger<T>();
        }

        public static ILogger GetLogger(string tag)
        {
            return LoggerManager.GetLogger(tag);
        }
    }
}
