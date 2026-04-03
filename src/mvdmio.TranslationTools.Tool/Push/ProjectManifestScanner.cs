using mvdmio.TranslationTools.Tool.Migrate;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class ProjectManifestScanner
{
   private readonly ResxMigrationScanner _scanner;
   private readonly ResxResourceSetParser _parser;
   private readonly Scaffolding.ManifestFileParser _manifestFileParser = new();

   public ProjectManifestScanner()
      : this(new ResxMigrationScanner(), new ResxResourceSetParser())
   {
   }

   internal ProjectManifestScanner(ResxMigrationScanner scanner, ResxResourceSetParser parser)
   {
      _scanner = scanner;
      _parser = parser;
   }

    public ProjectManifestScanResult ScanProject(string projectDirectory, string defaultLocale)
    {
      ResxMigrationScanResult? scanResult = null;

      try
      {
         scanResult = _scanner.ScanProject(projectDirectory);
      }
      catch (InvalidOperationException) when (!Directory.GetFiles(projectDirectory, "*.resx", SearchOption.AllDirectories).Any())
      {
      }

      if (scanResult is null)
         return ScanManifestFiles(projectDirectory);

      var items = scanResult.SourceFiles
         .Select(_parser.Parse)
         .SelectMany(parsedFile => parsedFile.Entries.Select(entry => new ProjectTranslationPushItem {
            Origin = BuildOrigin(parsedFile.SourceFile),
            Locale = parsedFile.SourceFile.Locale ?? ResxMigrationScanner.NormalizeLocale(defaultLocale),
            Key = entry.Key,
            Value = entry.Value
         }))
         .OrderBy(static item => item.Origin, StringComparer.OrdinalIgnoreCase)
         .ThenBy(static item => item.Key, StringComparer.Ordinal)
         .ThenBy(static item => item.Locale, StringComparer.Ordinal)
         .ToArray();

      return new ProjectManifestScanResult {
         FoundManifest = items.Length > 0,
         Items = items
      };
     }

   private ProjectManifestScanResult ScanManifestFiles(string projectDirectory)
   {
      var items = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
         .SelectMany(filePath => ScanManifestFile(filePath))
         .GroupBy(static item => item.Key, StringComparer.Ordinal)
         .Select(static group => {
            var values = group.Select(static x => x.DefaultValue).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length > 1)
               throw new InvalidOperationException($"Conflicting default values for translation key '{group.Key}'.");

            return new ProjectTranslationPushItem {
               Origin = "/Localizations.resx",
               Locale = "en",
               Key = group.Key,
               Value = values.FirstOrDefault()
            };
         })
         .OrderBy(static item => item.Key, StringComparer.Ordinal)
         .ToArray();

      return new ProjectManifestScanResult {
         FoundManifest = items.Length > 0,
         Items = items
      };
   }

   private IReadOnlyCollection<Scaffolding.ManifestPropertyDefinition> ScanManifestFile(string filePath)
   {
      var content = File.ReadAllText(filePath);
      var matches = System.Text.RegularExpressions.Regex.Matches(content, @"partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
      return matches.Select(match => match.Groups["name"].Value)
         .Where(static name => !string.IsNullOrWhiteSpace(name))
         .Distinct(StringComparer.Ordinal)
         .SelectMany(className => _manifestFileParser.Parse(content, className, Client.TranslationKeyNaming.UnderscoreToDot))
         .ToArray();
   }

   public ProjectManifestScanResult ScanProject(string projectDirectory)
   {
      return ScanProject(projectDirectory, "en");
   }

   internal static string BuildOrigin(ResxMigrationSourceFile sourceFile)
   {
      var resourceSetPath = sourceFile.ResourceSetPath.Replace('\\', '/');
      return "/" + resourceSetPath + ".resx";
   }
}

internal sealed class ProjectManifestScanResult
{
   public required bool FoundManifest { get; init; }
   public required IReadOnlyCollection<ProjectTranslationPushItem> Items { get; init; }
}

internal sealed class ProjectTranslationPushItem
{
   public required string Origin { get; init; }
   public required string Locale { get; init; }
   public required string Key { get; init; }
   public string? Value { get; init; }
   public string? DefaultValue => Value;
}
