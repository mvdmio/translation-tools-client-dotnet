using mvdmio.TranslationTools.Tool.Migrate;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class ProjectManifestScanner
{
   private readonly ResxMigrationScanner _scanner;
   private readonly ResxResourceSetParser _parser;

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
      var normalizedDefaultLocale = ResxMigrationScanner.NormalizeLocale(defaultLocale);
      var scanResult = _scanner.ScanProject(projectDirectory);

      var parsedFiles = scanResult.SourceFiles
         .Select(_parser.Parse)
         .ToArray();

      var items = parsedFiles
         .GroupBy(static parsedFile => parsedFile.SourceFile.ResourceSetName, StringComparer.Ordinal)
         .SelectMany(resourceSet => BuildResourceSetItems(resourceSet, normalizedDefaultLocale))
         .OrderBy(static item => item.Origin, StringComparer.OrdinalIgnoreCase)
         .ThenBy(static item => item.Key, StringComparer.Ordinal)
         .ThenBy(static item => item.Locale, StringComparer.Ordinal)
         .ToArray();

      return new ProjectManifestScanResult
      {
         Items = items
      };
   }

   private static IReadOnlyCollection<ProjectTranslationPushItem> BuildResourceSetItems(
      IGrouping<string, ResxParsedFile> resourceSet,
      string defaultLocale
   )
   {
      var filesByLocale = resourceSet
         .ToDictionary(
            file => file.SourceFile.Locale ?? defaultLocale,
            StringComparer.Ordinal
         );

      var keys = filesByLocale.Values
         .SelectMany(static file => file.Entries.Select(static entry => entry.Key))
         .Distinct(StringComparer.Ordinal)
         .OrderBy(static key => key, StringComparer.Ordinal)
         .ToArray();

      return filesByLocale
         .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
         .SelectMany(
            pair => keys.Select(
               key => new ProjectTranslationPushItem
               {
                  Origin = BuildOrigin(pair.Value.SourceFile),
                  Locale = pair.Key,
                  Key = key,
                  Value = pair.Value.Entries.FirstOrDefault(entry => entry.Key == key)?.Value
               }
            )
         )
         .ToArray();
   }

   internal static string BuildOrigin(ResxMigrationSourceFile sourceFile)
   {
      var resourceSetPath = sourceFile.ResourceSetPath.Replace('\\', '/');
      return "/" + resourceSetPath + ".resx";
   }
}

internal sealed class ProjectManifestScanResult
{
   public required IReadOnlyCollection<ProjectTranslationPushItem> Items { get; init; }
}

internal sealed class ProjectTranslationPushItem
{
   public required string Origin { get; init; }
   public required string Locale { get; init; }
   public required string Key { get; init; }
   public string? Value { get; init; }
}
