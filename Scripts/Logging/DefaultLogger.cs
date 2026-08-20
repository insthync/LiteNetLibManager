using Cysharp.Text;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace LiteNetLibManager
{
    public class DefaultLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly StreamWriter _infoWriter;
        private readonly StreamWriter _warnWriter;
        private readonly StreamWriter _errorWriter;

        // Shared across all DefaultLogger instances so multiple categories
        // writing to the same log-file prefix don't open the file twice or
        // stomp on each other.
        private static readonly ConcurrentDictionary<string, StreamWriter> _writers = new ConcurrentDictionary<string, StreamWriter>();

        // One lock per underlying writer, so different log files aren't
        // serialized against each other while still being safe for
        // concurrent writes to the *same* file.
        private static readonly ConcurrentDictionary<StreamWriter, object> _writerLocks = new ConcurrentDictionary<StreamWriter, object>();

        public DefaultLogger(string categoryName, string logFilePathPrefix)
        {
            _categoryName = categoryName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(logFilePathPrefix))
            {
                _infoWriter = GetOrCreateWriter(logFilePathPrefix, "info");
                _warnWriter = GetOrCreateWriter(logFilePathPrefix, "warn");
                _errorWriter = GetOrCreateWriter(logFilePathPrefix, "error");
            }
        }

        private static StreamWriter GetOrCreateWriter(string prefix, string level)
        {
            // One file per day per prefix per level, e.g. "Logs/Server_info_2026-08-20.log"
            string fileName = $"{prefix}_{DateTime.Now:yyyy-MM-dd}.{level}.log";

            return _writers.GetOrAdd(fileName, key =>
            {
                string dir = Path.GetDirectoryName(key);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var stream = new FileStream(key, FileMode.Append, FileAccess.Write, FileShare.Read);
                var writer = new StreamWriter(stream, Encoding.UTF8)
                {
                    AutoFlush = true // trade a little perf for durability; flip off + flush periodically if you need higher throughput
                };
                _writerLocks[writer] = new object();
                return writer;
            });
        }

        private static void WriteToFile(StreamWriter writer, string text)
        {
            if (writer == null)
                return;

            lock (_writerLocks[writer])
            {
                writer.WriteLine(text);
            }
        }

        public void LogInformation(string message, params object[] args)
        {
            var builder = new Utf16ValueStringBuilder(false);
            builder.AppendFormat(" INFO {0} [{1}] - ", _categoryName, DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendFormat(message, args);
            string text = builder.ToString();
            bool debugging = _infoWriter == null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugging = true;
#endif
            if (debugging)
            {
#if UNITY_2017_1_OR_NEWER
                UnityEngine.Debug.Log(text);
#else
                Console.WriteLine(text);
#endif
            }
            WriteToFile(_infoWriter, text);
        }

        public void LogError(string message, params object[] args)
        {
            var builder = new Utf16ValueStringBuilder(false);
            builder.AppendFormat("ERROR {0} [{1}] - ", _categoryName, DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendFormat(message, args);
            string text = builder.ToString();
            bool debugging = _warnWriter == null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugging = true;
#endif
            if (debugging)
            {
#if UNITY_2017_1_OR_NEWER
                UnityEngine.Debug.LogError(text);
#else
                Console.WriteLine(text);
#endif
            }
            WriteToFile(_errorWriter, text);
        }

        public void LogWarning(string message, params object[] args)
        {
            var builder = new Utf16ValueStringBuilder(false);
            builder.AppendFormat(" WARN {0} [{1}] - ", _categoryName, DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendFormat(message, args);
            string text = builder.ToString();
            bool debugging = _warnWriter == null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugging = true;
#endif
            if (debugging)
            {
#if UNITY_2017_1_OR_NEWER
                UnityEngine.Debug.LogWarning(text);
#else
                Console.WriteLine(text);
#endif
            }
            WriteToFile(_warnWriter, text);
        }
    }
}
