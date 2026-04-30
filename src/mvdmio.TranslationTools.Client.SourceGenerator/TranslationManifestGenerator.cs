using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : IIncrementalGenerator
{
   public void Initialize(IncrementalGeneratorInitializationContext context)
   {
      var analyzerOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => new GeneratorOptions {
         ProjectDirectory = GetProjectDirectory(provider.GlobalOptions),
         ProjectName = GetProjectName(provider.GlobalOptions),
         RootNamespace = GetGlobalOption(provider.GlobalOptions, "build_property.RootNamespace")
      });

      var allResxFiles = context.AdditionalTextsProvider
         .Where(file => file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         .Collect();

      var manifests = allResxFiles
         .Combine(analyzerOptions)
         .SelectMany(static (input, cancellationToken) => BuildManifests(input.Left, input.Right, cancellationToken));

      context.RegisterSourceOutput(manifests, static (productionContext, result) =>
      {
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

   private static ImmutableArray<TranslationManifestResult> BuildManifests(ImmutableArray<AdditionalText> files, GeneratorOptions options, CancellationToken cancellationToken)
   {
      if (files.IsDefaultOrEmpty)
         return ImmutableArray<TranslationManifestResult>.Empty;

      // Group files by base resx (neutral) path. Locale-suffixed files share the same base file name.
      var groups = new Dictionary<string, GroupBuilder>(StringComparer.OrdinalIgnoreCase);

      foreach (var file in files)
      {
         cancellationToken.ThrowIfCancellationRequested();

         var directory = GetDirectoryName(NormalizePath(file.Path));
         var fileName = GetFileName(file.Path);
         string baseFileName;
         string? localeSuffix;

         if (TryGetLocaleSuffix(file.Path, out var trimmedFileName, out var suffix))
         {
            baseFileName = trimmedFileName!;
            localeSuffix = suffix;
         }
         else
         {
            baseFileName = fileName;
            localeSuffix = null;
         }

         var groupKey = (string.IsNullOrEmpty(directory) ? string.Empty : directory + "/") + baseFileName;

         if (!groups.TryGetValue(groupKey, out var group))
         {
            group = new GroupBuilder { GroupKey = groupKey };
            groups[groupKey] = group;
         }

         if (localeSuffix is null)
            group.NeutralFile = file;
         else
            group.LocaleFiles.Add((localeSuffix!, file));
      }

      var results = new List<TranslationManifestResult>(groups.Count);

      foreach (var group in groups.Values)
      {
         if (group.NeutralFile is null)
            continue;

         var result = BuildManifest(group, options, cancellationToken);
         if (result is not null)
            results.Add(result);
      }

      return results.ToImmutableArray();
   }

   private static TranslationManifestResult? BuildManifest(GroupBuilder group, GeneratorOptions options, CancellationToken cancellationToken)
   {
      var neutralFile = group.NeutralFile!;
      var text = neutralFile.GetText(cancellationToken)?.ToString();
      if (string.IsNullOrWhiteSpace(text))
         return null;

      var entries = ReadResxEntries(text!);
      if (entries.Count == 0)
         return null;

      // Read locale resx values into a map: key -> (locale -> value)
      var localeValuesByKey = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
      foreach (var (locale, file) in group.LocaleFiles)
      {
         var localeText = file.GetText(cancellationToken)?.ToString();
         if (string.IsNullOrWhiteSpace(localeText))
            continue;

         foreach (var entry in ReadResxEntries(localeText!))
         {
            if (string.IsNullOrEmpty(entry.Value))
               continue;

            if (!localeValuesByKey.TryGetValue(entry.Key, out var perLocale))
            {
               perLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
               localeValuesByKey[entry.Key] = perLocale;
            }

            perLocale[locale] = entry.Value!;
         }
      }

      var relativePath = BuildProjectRelativePath(neutralFile.Path, options.ProjectDirectory);
      if (!IsValidProjectName(options.ProjectName))
      {
         return new TranslationManifestResult
         {
            Diagnostics = ImmutableArray.Create(Diagnostic.Create(
               TranslationGeneratorDiagnostics.InvalidProjectName,
               Location.None,
               options.ProjectName
            ))
         };
      }

      var origin = BuildOrigin(options.ProjectName, relativePath);
      var typeName = BuildTypeName(neutralFile.Path);
      var @namespace = BuildNamespace(relativePath, options.RootNamespace);

      return new TranslationManifestResult
      {
         Model = new TranslationManifestModel
         {
            Namespace = @namespace,
            TypeName = typeName,
            Origin = origin,
            Accessibility = "public",
            Properties = entries
                .GroupBy(entry => SanitizeIdentifier(entry.Key), StringComparer.Ordinal)
                .Select(g => g.First())
                .Select(entry => new TranslationManifestPropertyModel
                {
                   Name = SanitizeIdentifier(entry.Key),
                   Key = entry.Key,
                   DefaultValue = entry.Value,
                   LocaleValues = BuildLocaleValues(entry.Key, localeValuesByKey)
                })
               .ToImmutableArray()
         }
      };
   }

   private static ImmutableArray<TranslationManifestLocaleValueModel> BuildLocaleValues(string key, Dictionary<string, Dictionary<string, string>> localeValuesByKey)
   {
      var result = ImmutableArray.CreateBuilder<TranslationManifestLocaleValueModel>();

      if (localeValuesByKey.TryGetValue(key, out var perLocale))
      {
         foreach (var pair in perLocale.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
            result.Add(new TranslationManifestLocaleValueModel { Locale = pair.Key, Value = pair.Value });
      }

      return result.ToImmutable();
   }

   private static List<(string Key, string? Value)> ReadResxEntries(string text)
   {
      var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
      return document.Root?
         .Elements("data")
         .Select(static element => (
            Key: ((string?)element.Attribute("name") ?? string.Empty).Trim(),
            Value: NormalizeValue(element.Element("value")?.Value)
         ))
         .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
         .ToList() ?? new List<(string Key, string? Value)>();
   }

   private static string BuildHintName(TranslationManifestModel model)
   {
      return string.IsNullOrWhiteSpace(model.Namespace)
         ? model.TypeName + ".Translations"
         : model.Namespace + "." + model.TypeName + ".Translations";
   }

   private static string BuildOrigin(string projectName, string relativePath)
   {
      var normalizedRelativePath = NormalizePath(relativePath);
      var fileName = GetFileName(normalizedRelativePath);
      var directory = GetDirectoryName(normalizedRelativePath);
      var baseFileName = TryGetLocaleSuffix(fileName, out var trimmedFileName, out _) ? trimmedFileName : fileName;
      var resourcePath = string.IsNullOrWhiteSpace(directory)
         ? "/" + baseFileName
         : "/" + directory + "/" + baseFileName;

      return projectName + ":" + resourcePath;
   }

   private static string BuildTypeName(string path)
   {
      var fileName = GetFileName(path);
      var baseFileName = TryGetLocaleSuffix(fileName, out var trimmedFileName, out _) ? trimmedFileName : fileName;
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

      if (!IsAbsolutePath(path))
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

   private static bool IsAbsolutePath(string path)
   {
      if (string.IsNullOrWhiteSpace(path))
         return false;

      if (Path.IsPathRooted(path))
         return true;

      var normalizedPath = NormalizePath(path);
      return normalizedPath.Length >= 3
         && char.IsLetter(normalizedPath[0])
         && normalizedPath[1] == ':'
         && normalizedPath[2] == '/';
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

   private static string GetFileNameWithoutExtension(string path)
   {
      var fileName = GetFileName(path);
      return string.IsNullOrWhiteSpace(fileName)
         ? string.Empty
         : Path.GetFileNameWithoutExtension(fileName);
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

   private static string GetProjectName(AnalyzerConfigOptions options)
   {
      var projectName = GetGlobalOption(options, "build_property.MSBuildProjectName");
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetGlobalOption(options, "build_property.ProjectName");
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.MSBuildProjectFile"));
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.ProjectFileName"));
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      return GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.MSBuildProjectFullPath"));
   }

   private static bool IsValidProjectName(string projectName)
   {
      return !string.IsNullOrWhiteSpace(projectName) && !projectName.Contains(':');
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

   private static bool TryGetLocaleSuffix(string path, out string? baseFileName, out string? localeSuffix)
   {
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
      var separatorIndex = fileNameWithoutExtension.LastIndexOf('.');
      if (separatorIndex <= 0)
      {
         baseFileName = null;
         localeSuffix = null;
         return false;
      }

      var suffix = fileNameWithoutExtension.Substring(separatorIndex + 1);
      if (!suffix.All(static character => char.IsLetterOrDigit(character) || character == '-'))
      {
         baseFileName = null;
         localeSuffix = null;
         return false;
      }

      baseFileName = fileNameWithoutExtension.Substring(0, separatorIndex) + ".resx";
      localeSuffix = suffix;
      return true;
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }

   private sealed class GeneratorOptions
   {
      public string ProjectDirectory { get; set; } = string.Empty;
      public string ProjectName { get; set; } = string.Empty;
      public string RootNamespace { get; set; } = string.Empty;
   }

   private sealed class GroupBuilder
   {
      public string GroupKey { get; set; } = string.Empty;
      public AdditionalText? NeutralFile { get; set; }
      public List<(string Locale, AdditionalText File)> LocaleFiles { get; } = new();
   }
}
