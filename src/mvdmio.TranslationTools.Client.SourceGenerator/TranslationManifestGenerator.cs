using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : IIncrementalGenerator
{
   public void Initialize(IncrementalGeneratorInitializationContext context)
   {
      var manifests = context.AdditionalTextsProvider
         .Where(file => file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         .Select((file, cancellationToken) => BuildManifest(file, cancellationToken));

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

   private static TranslationManifestResult BuildManifest(AdditionalText file, CancellationToken cancellationToken)
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

      var origin = BuildOrigin(file.Path);
      var typeName = BuildTypeName(file.Path);
      var @namespace = BuildNamespace(file.Path);

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

   private static string BuildOrigin(string path)
   {
      var normalizedPath = path.Replace('\\', '/');
      var marker = "/src/";
      var markerIndex = normalizedPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
      var relativePath = markerIndex >= 0 ? normalizedPath.Substring(markerIndex + marker.Length) : Path.GetFileName(path);
      var fileName = Path.GetFileName(relativePath);
      var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
      string trimmedFileName;
      var baseFileName = TryGetLocaleSuffix(fileName, out trimmedFileName) ? trimmedFileName : fileName;

      return string.IsNullOrWhiteSpace(directory)
         ? "/" + baseFileName
         : "/" + directory + "/" + baseFileName;
   }

   private static string BuildTypeName(string path)
   {
      var fileName = Path.GetFileName(path);
      string trimmedFileName;
      var baseFileName = TryGetLocaleSuffix(fileName, out trimmedFileName) ? trimmedFileName : fileName;
      return Path.GetFileNameWithoutExtension(baseFileName).Replace(".", string.Empty);
   }

   private static string BuildNamespace(string path)
   {
      var normalizedPath = path.Replace('\\', '/');
      var marker = "/src/";
      var markerIndex = normalizedPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
      if (markerIndex < 0)
         return string.Empty;

      var relativePath = normalizedPath.Substring(markerIndex + marker.Length);
      var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '.') ?? string.Empty;
      if (string.IsNullOrWhiteSpace(directory))
         return string.Empty;

      var segments = directory.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(SanitizeIdentifier)
          .ToArray();
      return string.Join(".", segments);
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
}
