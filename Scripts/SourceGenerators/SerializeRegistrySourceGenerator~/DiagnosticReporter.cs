using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LiteNetLibManager.SourceGenerators
{
    public sealed class DiagnosticReporter : IDiagnosticReporter
    {
        readonly private GeneratorExecutionContext context;
        public DiagnosticReporter(GeneratorExecutionContext ctx)
        {
            context = ctx;
        }

        public void LogDebug(string message,
            [System.Runtime.CompilerServices.CallerFilePath]
            string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber]
            int sourceLineNumber = 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticHelper.CreateInfoDescriptor(message),
                DiagnosticHelper.GenerateExtenalLocation(sourceFilePath, sourceLineNumber)));
            Debug.LogDebug(message);
        }
        public void LogDebug(string message, Location location)
        {
            Debug.LogDebug(message);
        }

        public void LogInfo(string message,
            [System.Runtime.CompilerServices.CallerFilePath]
            string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber]
            int sourceLineNumber = 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticHelper.CreateInfoDescriptor(message),
                DiagnosticHelper.GenerateExtenalLocation(sourceFilePath, sourceLineNumber)));
            Debug.LogInfo(message);
        }
        public void LogInfo(string message, Location location)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.CreateInfoDescriptor(message), location));
            Debug.LogInfo(message);
        }
        public void LogWarning(string message, Location location)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.CreateWarningDescriptor(message), location));
            Debug.LogWarning(message);
        }
        public void LogWarning(string message,
            [System.Runtime.CompilerServices.CallerFilePath]
            string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber]
            int sourceLineNumber = 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticHelper.CreateWarningDescriptor(message),
                DiagnosticHelper.GenerateExtenalLocation(sourceFilePath, sourceLineNumber)));
            Debug.LogWarning(message);
        }
        public void LogError(string message, Location location)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.CreateErrorDescriptor(message), location));
            Debug.LogError(message, location.ToString());
        }
        public void LogError(string message,
            [System.Runtime.CompilerServices.CallerFilePath]
            string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber]
            int sourceLineNumber = 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticHelper.CreateErrorDescriptor(message),
                DiagnosticHelper.GenerateExtenalLocation(sourceFilePath, sourceLineNumber)));
            Debug.LogError(message, $"{sourceFilePath}:{sourceLineNumber}");
        }
        public void LogException(Exception e,
            [System.Runtime.CompilerServices.CallerFilePath]
            string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber]
            int sourceLineNumber = 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticHelper.CreateException(e),
                DiagnosticHelper.GenerateExtenalLocation(sourceFilePath, sourceLineNumber)));
            Debug.LogException(e);
        }
        public void LogException(Exception e, Location location)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.CreateException(e), location));
            Debug.LogException(e);
        }
    }

    internal static class DiagnosticHelper
    {
        static public DiagnosticDescriptor CreateErrorDescriptor(string message)
        {
            return new DiagnosticDescriptor(
                "LiteNetLibManager",
                "LiteNetLibManager Generator Error",
                message,
                "SourceGenerator",
                DiagnosticSeverity.Error, true,
                "an error occurred while generating serializers");
        }
        static public DiagnosticDescriptor CreateWarningDescriptor(string message)
        {
            return new DiagnosticDescriptor("LiteNetLibManager", "LiteNetLibManager Generator", message, "SourceGenerator", DiagnosticSeverity.Warning, true);
        }
        static public DiagnosticDescriptor CreateInfoDescriptor(string message)
        {
            return new DiagnosticDescriptor("LiteNetLibManager", "LiteNetLibManager Generator", message, "SourceGenerator", DiagnosticSeverity.Info, true);
        }
        static public DiagnosticDescriptor CreateException(Exception e)
        {
            var b = new StringBuilder();
            b.Append(e.Message);
            b.Append(e.StackTrace);
            return new DiagnosticDescriptor("LiteNetLibManager", "Unhandled Exception", b.ToString(), "SourceGenerator", DiagnosticSeverity.Error, true);
        }

        static public Location GenerateExtenalLocation(string sourceFile, int lineNo)
        {
            return Location.Create(sourceFile,
                TextSpan.FromBounds(0, 0),
                new LinePositionSpan(
                    new LinePosition(lineNo, 0),
                    new LinePosition(lineNo, 0)));
        }
    }


}
