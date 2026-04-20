using mvdmio.TranslationTools.Tool.Resx;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class ProjectManifestScanner
{
   private readonly ResxScanner _scanner;
   private readonly ResxResourceSetParser _parser;

   public ProjectManifestScanner()
      : this(new ResxScanner(), new ResxResourceSetParser())
   {
   }

   internal ProjectManifestScanner(ResxScanner scanner, ResxResourceSetParser parser)
   {
      _scanner = scanner;
      _parser = parser;
   }

   public ProjectManifestScanResult ScanProject(string projectName, string projectDirectory, string defaultLocale)
   {
      var normalizedDefaultLocale = ResxScanner.NormalizeLocale(defaultLocale);
      var scanResult = _scanner.ScanProject(projectDirectory);

      var parsedFiles = scanResult.SourceFiles
         .Select(_parser.Parse)
         .ToArray();

      var items = parsedFiles
          .GroupBy(static parsedFile => parsedFile.SourceFile.ResourceSetName, StringComparer.Ordinal)
         .SelectMany(resourceSet => BuildResourceSetItems(projectName, resourceSet, normalizedDefaultLocale))
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
      string projectName,
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
                  Origin = BuildOrigin(projectName, pair.Value.SourceFile),
                  Locale = pair.Key,
                  Key = key,
                  Value = pair.Value.Entries.FirstOrDefault(entry => entry.Key == key)?.Value
               }
            )
         )
         .ToArray();
   }

   internal static string BuildOrigin(string projectName, ResxSourceFile sourceFile)
   {
      var resourceSetPath = sourceFile.ResourceSetPath.Replace('\\', '/');
      return projectName + ":/" + resourceSetPath + ".resx";
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
