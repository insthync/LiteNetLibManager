﻿using Cysharp.Text;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Generic;
using ZLogger;

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
            _defaultLogger = loggerFactory.CreateLogger("No Tag");
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
        public static LoggerManager DefaultLoggerManager { get; set; }
        public static LoggerManager WarningLoggerManager { get; set; }
        public static LoggerManager ErrorLoggerManager { get; set; }

        static LogManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            DefaultLoggerManager = new LoggerManager(LoggerFactory.Create(builder =>
            {
                // or more configuration, you can use builder.AddFilter
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddZLoggerConsole((options) =>
                {
                    options.PrefixFormatter = PrefixFormatterConfigure;
                });
            }));

            DefaultLoggerManager.Logger.LogInformation("===== Logger Initialized =====");
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

        public static bool IsLoggerDisposed => DefaultLoggerManager.IsDisposed;
        public static bool IsWarningLoggerDisposed => WarningLoggerManager != null ? WarningLoggerManager.IsDisposed : IsLoggerDisposed;
        public static bool IsErrorLoggerDisposed => ErrorLoggerManager != null ? ErrorLoggerManager.IsDisposed : IsLoggerDisposed;

        public static ILogger Logger => DefaultLoggerManager.Logger;
        public static ILogger WarningLogger => WarningLoggerManager != null ? WarningLoggerManager.Logger : Logger;
        public static ILogger ErrorLogger => ErrorLoggerManager != null ? ErrorLoggerManager.Logger : Logger;

        public static ILogger<T> GetLogger<T>() where T : class
        {
            return DefaultLoggerManager.GetLogger<T>();
        }

        public static ILogger GetLogger(string tag)
        {
            return DefaultLoggerManager.GetLogger(tag);
        }

        public static ILogger<T> GetWarningLogger<T>() where T : class
        {
            if (WarningLoggerManager == null)
                return GetLogger<T>();
            return WarningLoggerManager.GetLogger<T>();
        }

        public static ILogger GetWarningLogger(string tag)
        {
            if (WarningLoggerManager == null)
                return GetLogger(tag);
            return WarningLoggerManager.GetLogger(tag);
        }

        public static ILogger<T> GetErrorLogger<T>() where T : class
        {
            if (ErrorLoggerManager == null)
                return GetLogger<T>();
            return ErrorLoggerManager.GetLogger<T>();
        }

        public static ILogger GetErrorLogger(string tag)
        {
            if (ErrorLoggerManager == null)
                return GetLogger(tag);
            return ErrorLoggerManager.GetLogger(tag);
        }
    }
}
