using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LiteNetLibManager.SourceGenerators
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            if (!Helpers.IsBuildTime)
                return;
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!Helpers.IsBuildTime)
                return;

            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            Helpers.SetupContext(context);
            var diagnostic = new DiagnosticReporter(context);
            diagnostic.LogInfo($"Begin Processing assembly {context.Compilation.AssemblyName}");

            // Get INetSerializable interface symbol
            var interfaceSymbol = context.Compilation.GetTypeByMetadataName("LiteNetLib.Utils.INetSerializable");
            if (interfaceSymbol == null)
                return;

            var serializableTypes = new List<TypeInfo>();
            foreach (var typeSyntax in receiver.CandidateTypes)
            {
                var semanticModel = context.Compilation.GetSemanticModel(typeSyntax.SyntaxTree);
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax) as INamedTypeSymbol;

                if (typeSymbol == null || typeSymbol.IsAbstract)
                    continue;

                // Check if type implements INetSerializable
                if (typeSymbol.AllInterfaces.Contains(interfaceSymbol, SymbolEqualityComparer.Default))
                {
                    var fullTypeName = GetFullTypeName(typeSymbol);
                    var info = new TypeInfo
                    {
                        FullTypeName = fullTypeName,
                        TypeName = typeSymbol.Name,
                        IsValueType = typeSymbol.IsValueType,
                        IsGenericType = typeSymbol.IsGenericType,
                        HasParameterlessConstructor = HasParameterlessConstructor(typeSymbol)
                    };
                    serializableTypes.Add(info);
                }
            }

            if (serializableTypes.Count > 0)
            {
                var writerSource = GenerateWriterRegistration(serializableTypes);
                AddGeneratedSources(context, "WriterRegistry", writerSource);

                var readerSource = GenerateReaderRegistration(serializableTypes);
                AddGeneratedSources(context, "ReaderRegistry", readerSource);
            }

            diagnostic.LogInfo($"End Processing assembly {context.Compilation.AssemblyName}.");
        }

        public static void AddGeneratedSources(GeneratorExecutionContext context, string className, string codes)
        {
            if (string.IsNullOrWhiteSpace(codes))
                return;

            context.CancellationToken.ThrowIfCancellationRequested();
            string fileName = $"{className}.{context.Compilation.AssemblyName}.generated.cs";
            var sourceText = SourceText.From(codes, Encoding.UTF8);
            var sourcePath = Path.Combine(Helpers.GetOutputPath(), fileName);
            Debug.LogInfo($"output {fileName} to {sourcePath}");
            try
            {
                if (Helpers.CanWriteFiles)
                    File.WriteAllText(sourcePath, sourceText.ToString());
            }
            catch (Exception e)
            {
                //In the rare event/occasion when this happen, at the very least don't bother the user and move forward
                Debug.LogWarning($"cannot write file {Path.Combine(Helpers.GetOutputPath(), sourcePath)}. An exception has been thrown:{e}");
            }
            context.AddSource(fileName, sourceText);
        }

        private bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.IsValueType)
                return true; // Structs always have parameterless constructor

            return typeSymbol.Constructors.Any(c =>
                c.Parameters.Length == 0 &&
                c.DeclaredAccessibility == Accessibility.Public);
        }

        private string GetFullTypeName(INamedTypeSymbol typeSymbol)
        {
            var parts = new List<string>();

            // Collect nested type names
            var current = typeSymbol;
            while (current != null)
            {
                parts.Insert(0, current.Name);
                current = current.ContainingType;
            }

            // Add namespace if needed
            if (ShouldIncludeNamespace(typeSymbol))
            {
                string namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();
                parts.Insert(0, namespaceName);
            }

            return string.Join(".", parts);
        }

        private bool ShouldIncludeNamespace(INamedTypeSymbol typeSymbol)
        {
            // Don't include if it's global namespace
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
                return false;

            string namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();

            // Don't include if it's exactly "LiteNetLibManager"
            if (namespaceName == "LiteNetLibManager")
                return false;

            return true;
        }

        private string GenerateWriterRegistration(List<TypeInfo> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System;");
            sb.AppendLine("using LiteNetLib.Utils;");
            sb.AppendLine();
            sb.AppendLine("namespace LiteNetLibManager.Serialization");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class WriterRegistry");
            sb.AppendLine("    {");

            Dictionary<string, TypeInfo> filteredTypes = new Dictionary<string, TypeInfo>();
            foreach (var info in types)
            {
                if (info.IsGenericType)
                    continue;

                if (!filteredTypes.ContainsKey(info.FullTypeName))
                {
                    filteredTypes.Add(info.FullTypeName, info);
                }
            }

            // Generate writer methods
            foreach (var info in filteredTypes.Values)
            {
                sb.AppendLine($"        [WriterRegister(typeof({info.FullTypeName}))]");
                sb.AppendLine($"        public static void Write_{info.FullTypeName.Replace('.', '_')}(NetDataWriter writer, object value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            writer.Put<{info.FullTypeName}>(({info.FullTypeName})value);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateReaderRegistration(List<TypeInfo> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System;");
            sb.AppendLine("using LiteNetLib.Utils;");
            sb.AppendLine();
            sb.AppendLine("namespace LiteNetLibManager.Serialization");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class ReaderRegistry");
            sb.AppendLine("    {");

            Dictionary<string, TypeInfo> filteredTypes = new Dictionary<string, TypeInfo>();
            foreach (var info in types)
            {
                if (info.IsGenericType)
                    continue;

                if (!filteredTypes.ContainsKey(info.FullTypeName))
                {
                    filteredTypes.Add(info.FullTypeName, info);
                }
            }

            // Generate reader methods
            foreach (var info in filteredTypes.Values)
            {
                sb.AppendLine($"        [ReaderRegister(typeof({info.FullTypeName}))]");
                sb.AppendLine($"        public static object Read_{info.FullTypeName.Replace('.', '_')}(NetDataReader reader)");
                sb.AppendLine("        {");

                if (info.IsValueType)
                {
                    // Struct: use reader.Get<T>()
                    sb.AppendLine($"            return reader.Get<{info.FullTypeName}>();");
                }
                else if (info.HasParameterlessConstructor)
                {
                    sb.AppendLine($"            return reader.Get<{info.FullTypeName}>(() => new {info.FullTypeName}());");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private class TypeInfo
        {
            public string FullTypeName { get; set; }
            public string TypeName { get; set; }
            public bool IsValueType { get; set; }
            public bool IsGenericType { get; set; }
            public bool HasParameterlessConstructor { get; set; }
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<TypeDeclarationSyntax> CandidateTypes { get; } = new List<TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Look for class or struct declarations
                if (syntaxNode is ClassDeclarationSyntax classDeclaration)
                {
                    CandidateTypes.Add(classDeclaration);
                }
                else if (syntaxNode is StructDeclarationSyntax structDeclaration)
                {
                    CandidateTypes.Add(structDeclaration);
                }
            }
        }
    }
}