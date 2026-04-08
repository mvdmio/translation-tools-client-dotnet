using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : IIncrementalGenerator
{
   public void Initialize(IncrementalGeneratorInitializationContext context)
   {
      var analyzerOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => new GeneratorOptions {
         ProjectDirectory = GetProjectDirectory(provider.GlobalOptions),
         RootNamespace = GetGlobalOption(provider.GlobalOptions, "build_property.RootNamespace")
      });

      var manifests = context.AdditionalTextsProvider
         .Where(file => file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         .Combine(analyzerOptions)
         .Select(static (input, cancellationToken) => BuildManifest(input.Left, input.Right, cancellationToken));

      context.RegisterSourceOutput(manifests, static (productionContext, result) => {
         foreach (var diagnostic in result.Diagnostics)
            productionContext.ReportDiagnostic(diagnostic);

         if (result.Model is null)
            return;

         productionContext.AddSource(
            hintName: BuildHintName(result.Model) + ".g.cs",
            source: TranslationManifestEmitter.Emit(result.Model)
          );
      });
   }

   private static TranslationManifestResult BuildManifest(AdditionalText file, GeneratorOptions options, CancellationToken cancellationToken)
   {
      var text = file.GetText(cancellationToken)?.ToString();
      if (string.IsNullOrWhiteSpace(text))
         return new TranslationManifestResult();

      if (TryGetLocaleSuffix(file.Path, out _))
         return new TranslationManifestResult();

      var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
      var entries = document.Root?
         .Elements("data")
         .Select(static element => (
            Key: ((string?)element.Attribute("name") ?? string.Empty).Trim(),
            Value: NormalizeValue(element.Element("value")?.Value)
         ))
         .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
         .ToArray() ?? new (string Key, string? Value)[0];

      if (entries.Length == 0)
         return new TranslationManifestResult();

      var relativePath = BuildProjectRelativePath(file.Path, options.ProjectDirectory);
      var origin = BuildOrigin(relativePath);
      var typeName = BuildTypeName(file.Path);
      var @namespace = BuildNamespace(relativePath, options.RootNamespace);

      return new TranslationManifestResult {
         Model = new TranslationManifestModel {
            Namespace = @namespace,
            TypeName = typeName,
            Origin = origin,
            Accessibility = "public",
             Properties = entries
                .GroupBy(entry => SanitizeIdentifier(entry.Key), StringComparer.Ordinal)
                .Select(group => group.First())
                .Select(entry => new TranslationManifestPropertyModel {
                   Name = SanitizeIdentifier(entry.Key),
                   Key = entry.Key,
                   DefaultValue = entry.Value
               })
               .ToImmutableArray()
         }
      };
   }

   private static string BuildHintName(TranslationManifestModel model)
   {
      return string.IsNullOrWhiteSpace(model.Namespace)
         ? model.TypeName + ".Translations"
         : model.Namespace + "." + model.TypeName + ".Translations";
   }

   private static string BuildOrigin(string relativePath)
   {
      var normalizedRelativePath = NormalizePath(relativePath);
      var fileName = GetFileName(normalizedRelativePath);
      var directory = GetDirectoryName(normalizedRelativePath);
      string? trimmedFileName;
      var baseFileName = TryGetLocaleSuffix(fileName, out trimmedFileName) ? trimmedFileName : fileName;

      return string.IsNullOrWhiteSpace(directory)
         ? "/" + baseFileName
         : "/" + directory + "/" + baseFileName;
   }

   private static string BuildTypeName(string path)
   {
      var fileName = GetFileName(path);
      string? trimmedFileName;
      var baseFileName = TryGetLocaleSuffix(fileName, out trimmedFileName) ? trimmedFileName : fileName;
      return Path.GetFileNameWithoutExtension(baseFileName).Replace(".", string.Empty);
   }

   private static string BuildNamespace(string relativePath, string rootNamespace)
   {
      var sanitizedRootNamespace = string.Join(".", rootNamespace
         .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(SanitizeIdentifier)
      );

      var directory = GetDirectoryName(relativePath);
      if (string.IsNullOrWhiteSpace(directory))
         return sanitizedRootNamespace;

      var directorySegments = directory
         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(SanitizeIdentifier);

      if (string.IsNullOrWhiteSpace(sanitizedRootNamespace))
         return string.Join(".", directorySegments);

      return string.Join(".", new[] { sanitizedRootNamespace }.Concat(directorySegments));
   }

   private static string BuildProjectRelativePath(string path, string projectDirectory)
   {
      var normalizedPath = NormalizePath(path);
      var normalizedProjectDirectory = NormalizePath(projectDirectory);

      if (!Path.IsPathRooted(path))
         return normalizedPath.TrimStart('/');

      if (!string.IsNullOrWhiteSpace(projectDirectory))
      {
         var projectPath = EnsureTrailingSeparator(normalizedProjectDirectory);
         var fullPath = normalizedPath;

         if (fullPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(projectPath.Length);
      }

      return GetFileName(normalizedPath);
   }

   private static string EnsureTrailingSeparator(string path)
   {
      if (path.EndsWith("/", StringComparison.Ordinal))
         return path;

      return path + "/";
   }

   private static string NormalizePath(string path)
   {
      return path.Replace("\\", "/");
   }

   private static string GetFileName(string path)
   {
      var normalizedPath = NormalizePath(path);
      var separatorIndex = normalizedPath.LastIndexOf('/');
      return separatorIndex >= 0
         ? normalizedPath.Substring(separatorIndex + 1)
         : normalizedPath;
   }

   private static string GetDirectoryName(string path)
   {
      var normalizedPath = NormalizePath(path).TrimEnd('/');
      var separatorIndex = normalizedPath.LastIndexOf('/');
      if (separatorIndex <= 0)
         return string.Empty;

      return normalizedPath.Substring(0, separatorIndex);
   }

   private static string GetGlobalOption(AnalyzerConfigOptions options, string key)
   {
      return options.TryGetValue(key, out var value)
         ? value
         : string.Empty;
   }

   private static string GetProjectDirectory(AnalyzerConfigOptions options)
   {
      var projectDirectory = GetGlobalOption(options, "build_property.MSBuildProjectDirectory");
      if (!string.IsNullOrWhiteSpace(projectDirectory))
         return projectDirectory;

      return GetGlobalOption(options, "build_property.ProjectDir");
   }

   private static string SanitizeIdentifier(string value)
   {
      if (string.IsNullOrWhiteSpace(value))
         return "Value";

      var characters = value.Select(static character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
      var identifier = new string(characters).Trim('_');
      if (string.IsNullOrWhiteSpace(identifier))
         identifier = "Value";

      if (!char.IsLetter(identifier[0]) && identifier[0] != '_')
         identifier = "_" + identifier;

      return identifier;
   }

   private static bool TryGetLocaleSuffix(string path, out string? baseFileName)
   {
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
      var separatorIndex = fileNameWithoutExtension.LastIndexOf('.');
      if (separatorIndex <= 0)
      {
         baseFileName = null;
         return false;
      }

      var suffix = fileNameWithoutExtension.Substring(separatorIndex + 1);
      if (!suffix.All(static character => char.IsLetterOrDigit(character) || character == '-'))
      {
         baseFileName = null;
         return false;
      }

      baseFileName = fileNameWithoutExtension.Substring(0, separatorIndex) + ".resx";
      return true;
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }

   private sealed class GeneratorOptions
   {
      public string ProjectDirectory { get; set; } = string.Empty;
      public string RootNamespace { get; set; } = string.Empty;
   }
}
