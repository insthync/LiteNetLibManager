using Microsoft.CodeAnalysis;

namespace LiteNetLibManager.SourceGenerators
{
    public static class GlobalOptions
    {
        /// <summary>
        /// Override the current project path. Used by the generator to flush logs or lookup files.
        /// </summary>
        public const string ProjectPath = "litenetlibmanager.serializing.projectpath";
        /// <summary>
        /// Override the output folder where the generator flush logs and generated files.
        /// </summary>
        public const string OutputPath = "litenetlibmanager.serializing.outputfolder";
        /// <summary>
        /// Override the namespace used for generated code.
        /// </summary>
        public const string Namespace = "litenetlibmanager.serializing.namespace";
        /// <summary>
        /// Override the class name used for generated code.
        /// </summary>
        public const string ClassName = "litenetlibmanager.serializing.classname";
        /// <summary>
        /// Enable/Disable writing generated code to output folder
        /// </summary>
        public const string WriteFilesToDisk = "litenetlibmanager.serializing.write_files_to_disk";
        /// <summary>
        /// Enable/Disable writing logs to the file (default is Temp/LiteNetLibManager_SerializeRegistry/SourceGenerator.log)
        /// </summary>
        public const string WriteLogsToDisk = "litenetlibmanager.serializing.write_logs_to_disk";
        /// <summary>
        /// The minimal log level. Available: Debug, Warning, Error. Default is error. (NOT SUPPORTED YET)
        /// </summary>
        public const string LoggingLevel = "litenetlibmanager.serializing.logging_level";

        ///<summary>
        /// return if a flag is set in the GlobalOption dictionary.
        /// A flag is consider set if the key is in the GlobalOptions and its string value is either empty or "1"
        /// Otherwise the flag is considered as not set.
        ///</summary>
        public static bool GetOptionsFlag(this GeneratorExecutionContext context, string key, bool defaultValue = false)
        {
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key, out var stringValue))
                return string.IsNullOrEmpty(stringValue) || (stringValue is "1" or "true");
            return defaultValue;
        }

        /// <summary>
        /// Return the string value associated with the key in the GlobalOptions if the key is present
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string GetOptionsString(this GeneratorExecutionContext context, string key, string defaultValue = null)
        {
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key, out var stringValue))
                return stringValue;
            return defaultValue;
        }
    }
}
