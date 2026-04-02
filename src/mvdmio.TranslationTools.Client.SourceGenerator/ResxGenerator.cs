using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : ISourceGenerator
{
   private static readonly Regex LocaleSuffixRegex = new("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

   public void Initialize(GeneratorInitializationContext context)
   {
   }

   public void Execute(GeneratorExecutionContext context)
   {
      var projectDirectory = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDirectoryValue)
         ? projectDirectoryValue
         : string.Empty;
      var rootNamespace = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespaceValue)
         ? rootNamespaceValue
         : string.Empty;
      var files = context.AdditionalFiles
         .Where(static file => string.Equals(Path.GetExtension(file.Path), ".resx", System.StringComparison.OrdinalIgnoreCase))
         .ToImmutableArray();
      var result = Build(files, projectDirectory, rootNamespace);

      foreach (var diagnostic in result.Diagnostics)
         context.ReportDiagnostic(diagnostic);

      foreach (var type in result.Types)
         context.AddSource(BuildHintName(type), ResxGeneratorEmitter.Emit(type));
   }

   private static ResxGeneratorFileModel Build(ImmutableArray<AdditionalText> files, string projectDirectory, string rootNamespace)
   {
      var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
      var types = new List<ResxGeneratorTypeModel>();
      var resourceSetNames = new Dictionary<string, string>(System.StringComparer.Ordinal);

      foreach (var file in files.OrderBy(static x => x.Path, System.StringComparer.OrdinalIgnoreCase))
      {
         var designerPath = Path.Combine(Path.GetDirectoryName(file.Path) ?? string.Empty, Path.GetFileNameWithoutExtension(file.Path) + ".Designer.cs");
         if (File.Exists(designerPath))
            diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.StaleDesignerFile, Location.None, designerPath));

         if (TryGetLocaleSuffix(Path.GetFileNameWithoutExtension(file.Path), out _))
            continue;

         var relativePath = GetRelativePath(projectDirectory, file.Path);
         var resourceBaseName = Path.ChangeExtension(relativePath, null)!.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');

         if (resourceSetNames.ContainsKey(resourceBaseName))
         {
            diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.ConflictingResourceSet, Location.None, resourceBaseName, resourceSetNames[resourceBaseName], relativePath));
            continue;
         }

         resourceSetNames[resourceBaseName] = relativePath;

         var type = BuildType(file, projectDirectory, rootNamespace, relativePath, resourceBaseName, diagnostics);
         if (type is not null)
            types.Add(type);
      }

      return new ResxGeneratorFileModel
      {
         Types = types.ToImmutableArray(),
         Diagnostics = diagnostics.ToImmutable()
      };
   }

   private static ResxGeneratorTypeModel? BuildType(AdditionalText file, string projectDirectory, string rootNamespace, string relativePath, string resourceBaseName, ImmutableArray<Diagnostic>.Builder diagnostics)
   {
      var document = XDocument.Parse(file.GetText()?.ToString() ?? string.Empty, LoadOptions.PreserveWhitespace);
      var fileName = Path.GetFileNameWithoutExtension(relativePath);
      var directory = Path.GetDirectoryName(relativePath);
      var namespaceName = string.IsNullOrWhiteSpace(directory)
         ? rootNamespace
         : $"{rootNamespace}.{directory.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.')}";
      var typeName = fileName;
      var usedNames = new Dictionary<string, string>(System.StringComparer.Ordinal);
      var properties = ImmutableArray.CreateBuilder<ResxGeneratorPropertyModel>();

      foreach (var element in document.Root?.Elements("data") ?? [])
      {
         var key = (string?)element.Attribute("name");
         if (string.IsNullOrWhiteSpace(key))
            continue;

         var propertyName = ResxPropertyNameNormalizer.Normalize(key);
         if (usedNames.ContainsKey(propertyName))
         {
            diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.ConflictingPropertyName, Location.None, propertyName, usedNames[propertyName], key, relativePath));
            continue;
         }

         usedNames[propertyName] = key;

         properties.Add(new ResxGeneratorPropertyModel
         {
            Name = propertyName,
            Key = $"{resourceBaseName}.{key}",
            ResourceKey = key,
            DefaultValue = NormalizeValue(element.Element("value")?.Value)
         });
      }

      return new ResxGeneratorTypeModel
      {
         Namespace = namespaceName,
         TypeName = typeName,
         ResourceBaseName = resourceBaseName,
         Properties = properties.ToImmutable()
      };
   }

   private static string BuildHintName(ResxGeneratorTypeModel type)
   {
      return string.IsNullOrWhiteSpace(type.Namespace)
         ? type.TypeName + ".Translations.g.cs"
         : type.Namespace + "." + type.TypeName + ".Translations.g.cs";
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }

   private static bool TryGetLocaleSuffix(string fileName, out string? baseName)
   {
      var separatorIndex = fileName.LastIndexOf('.');
      if (separatorIndex <= 0)
      {
         baseName = null;
         return false;
      }

      var suffix = fileName.Substring(separatorIndex + 1);
      if (!LocaleSuffixRegex.IsMatch(suffix))
      {
         baseName = null;
         return false;
      }

      baseName = fileName.Substring(0, separatorIndex);
      return true;
   }

   private static string GetRelativePath(string relativeTo, string path)
   {
      var relativeUri = new System.Uri(AppendDirectorySeparator(relativeTo));
      var pathUri = new System.Uri(path);
      return System.Uri.UnescapeDataString(relativeUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
   }

   private static string AppendDirectorySeparator(string path)
   {
      if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), System.StringComparison.Ordinal)
          || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), System.StringComparison.Ordinal))
      {
         return path;
      }

      return path + Path.DirectorySeparatorChar;
   }
}
