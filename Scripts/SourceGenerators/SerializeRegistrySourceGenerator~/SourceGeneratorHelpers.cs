using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LiteNetLibManager.SourceGenerators
{
    /// <summary>
    /// Some helpers for debugging purpose. Notable entries:
    /// - Logging and reporting (in file and compiler diagnostics)
    /// </summary>
    internal static class Helpers
    {
        static private ThreadLocal<string> s_OutputFolder;
        static private ThreadLocal<string> s_ProjectPath;
        static private ThreadLocal<bool> s_WriteLogToDisk;
        static private ThreadLocal<bool> s_CanWriteFiles;
        static private ThreadLocal<LoggingLevel> s_LogLevel;

        public enum LoggingLevel : int
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
        }

        static public string ProjectPath
        {
            get => s_ProjectPath.Value;
            private set => s_ProjectPath.Value = value;
        }

        static public string OutputFolder
        {
            get => s_OutputFolder.Value;
            private set => s_OutputFolder.Value = value;
        }

        static public bool WriteLogToDisk
        {
            get => s_WriteLogToDisk.Value;
            private set => s_WriteLogToDisk.Value = value;
        }

        static public bool CanWriteFiles
        {
            get => s_CanWriteFiles.Value;
            private set => s_CanWriteFiles.Value = value;
        }

        static public LoggingLevel CurrentLogLevel => s_LogLevel.Value;

        /// <summary>
        /// Returns true if running as part of csc.exe, otherwise we are likely running in the IDE.
        /// Skipping Source Generation in the IDE can be a considerable performance win as source
        /// generators can be run multiple times per keystroke. If the user doesn't rely on generated types
        /// consider skipping your Generator's Execute method when this returns false
        /// </summary>
        public static bool IsBuildTime
        {
            // We want to be exclusive rather than inclusive here to avoid any issues with unknown processes, Unity changes, and testing
            get
            {
                Assembly assembly = Assembly.GetEntryAssembly();
                if (assembly == null)
                {
                    return false;
                }
                // VS Code
                if (assembly.FullName.StartsWith("Microsoft",
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
                // Visual Studio
                if (assembly.FullName.StartsWith("ServiceHub",
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
                // Rider
                if (assembly.FullName.StartsWith("JetBrains",
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                return true;
            }
        }

        static Helpers()
        {
            s_OutputFolder = new ThreadLocal<string>(() => Path.Combine("Temp", "LiteNetLibManager_SerializeRegistry"));
            s_ProjectPath = new ThreadLocal<string>();
            s_WriteLogToDisk = new ThreadLocal<bool>();
            s_CanWriteFiles = new ThreadLocal<bool>();
            s_LogLevel = new ThreadLocal<LoggingLevel>();
        }

        static public void SetupContext(GeneratorExecutionContext executionContext)
        {
            ProjectPath = null;
            //by default we allow both writing files and logs to disk. It is possible to change the behavior via
            //globalconfig
            CanWriteFiles = true;
            WriteLogToDisk = true;
            if (executionContext.AdditionalFiles.Any() && !string.IsNullOrEmpty(executionContext.AdditionalFiles[0].Path))
                ProjectPath = executionContext.AdditionalFiles[0].GetText()?.ToString();
            //Parse global options and overrides default behaviour. They are used by both tests, and Editor (2021_OR_NEWER)
            ProjectPath = executionContext.GetOptionsString(GlobalOptions.ProjectPath, ProjectPath);
            OutputFolder = executionContext.GetOptionsString(GlobalOptions.OutputPath, OutputFolder);

            //If the project path is not valid, for any reason, we can't write files and/or log to disk
            if (string.IsNullOrEmpty(ProjectPath))
            {
                WriteLogToDisk = false;
                CanWriteFiles = false;
                Debug.LogWarning("Unable to setup/find the project path. Forcibly disable writing logs and files to disk");
            }
            else
            {
                Directory.CreateDirectory(GetOutputPath());
                CanWriteFiles = executionContext.GetOptionsFlag(GlobalOptions.WriteFilesToDisk, CanWriteFiles);
                WriteLogToDisk = executionContext.GetOptionsFlag(GlobalOptions.WriteLogsToDisk, WriteLogToDisk);
            }

            //The default log level is info. User can customise that via debug config. Info level is very light right now.
            s_LogLevel.Value = LoggingLevel.Info;
            var loggingLevel = executionContext.GetOptionsString(GlobalOptions.LoggingLevel);
            if (!string.IsNullOrEmpty(loggingLevel))
            {
                if (Enum.TryParse<LoggingLevel>(loggingLevel, ignoreCase: true, out var logLevel))
                    s_LogLevel.Value = logLevel;
                else throw new InvalidOperationException($"Unable to parse {GlobalOptions.LoggingLevel}:{loggingLevel}!");
            }
        }

        public static string GetOutputPath()
        {
            return Path.Combine(ProjectPath, OutputFolder);
        }

        public static SourceText WithInitialLineDirective(this SourceText sourceText, string generatedSourceFilePath)
        {
            var firstLine = sourceText.Lines.FirstOrDefault();
            return sourceText.WithChanges(new TextChange(firstLine.Span, $"#line 1 \"{generatedSourceFilePath}\"" + Environment.NewLine + firstLine));
        }
    }

    internal static class Debug
    {
        public static string LastErrorLog { get; set; } // used for tests. TODO have something fancier that tracks all logs
        public static void LaunchDebugger()
        {
            if (Debugger.IsAttached)
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debugger.Launch();
            }
            else
            {
                string text = $"Attach to {Process.GetCurrentProcess().Id} netcode generator";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    StarProcess("/usr/bin/osascript", $"-e \"display dialog \\\"{text}\\\" with icon note buttons {{\\\"OK\\\"}}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    StarProcess("/usr/bin/zenity", $@"--info --title=""Attach Debugger"" --text=""{text}"" --no-wrap");
                }
            }
        }

        public static void LaunchDebugger(GeneratorExecutionContext context, string assembly)
        {
            if (string.IsNullOrEmpty(assembly)
               || string.IsNullOrEmpty(context.Compilation.AssemblyName)
               || context.Compilation.AssemblyName.Equals(assembly, StringComparison.InvariantCultureIgnoreCase))
            {
                LaunchDebugger();
            }
        }

        public static void LaunchDebugger(Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax node, string[] names)
        {
            if (names.Contains(node.Identifier.ValueText))
            {
                LaunchDebugger();
            }
        }

        private static void StarProcess(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            var processTemp = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            processTemp.Start();
            processTemp.WaitForExit();
        }

        private const string LogFile = "SourceGenerator.log";
        private static string GetLogFilePath()
        {
            return Path.Combine(Helpers.GetOutputPath(), LogFile);
        }
        private static TextWriter GetOutputStream()
        {
            return Helpers.WriteLogToDisk ? File.AppendText(GetLogFilePath()) : Console.Out;
        }
        static void LogToDebugStream(string level, string message)
        {
            try
            {
                using var writer = GetOutputStream();
                writer.WriteLine($"[{level}]{message}");
            }
            catch (Exception flushEx)
            {
                Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
            }
        }
        public static void LogException(Exception exception)
        {
            LastErrorLog = exception.ToString();
            try
            {
                using var writer = GetOutputStream();
                writer.Write("[Exception]");
                writer.WriteLine(exception.ToString());
                writer.WriteLine("Callstack:");
                writer.Write(exception.StackTrace);
                writer.Write('\n');
            }
            catch (Exception flushEx)
            {
                Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
            }
        }
        public static void LogDebug(string message)
        {
            if (Helpers.CurrentLogLevel > Helpers.LoggingLevel.Debug)
                return;
            LogToDebugStream("Debug", message);
        }
        public static void LogInfo(string message)
        {
            if (Helpers.CurrentLogLevel > Helpers.LoggingLevel.Info)
                return;
            LogToDebugStream("Info", message);
        }
        public static void LogWarning(string message)
        {
            if (Helpers.CurrentLogLevel > Helpers.LoggingLevel.Warning)
                return;
            LogToDebugStream("Warning", message);
        }
        public static void LogError(string message, string additionalInfo)
        {
            message += $"\n\nAdditional info:\n{additionalInfo}\n\nStacktrace:\n";
            message += Environment.StackTrace;
            LastErrorLog = message;
            LogToDebugStream("Error", message);
        }
    }
}
