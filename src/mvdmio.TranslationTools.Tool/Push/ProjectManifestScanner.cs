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
            static file => new IndexedFile(file.SourceFile, IndexEntries(file)),
            StringComparer.Ordinal
         );

      var keys = CollectKeys(filesByLocale, defaultLocale);

      return filesByLocale
         .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
         .SelectMany(
            pair => keys.Select(
               key => new ProjectTranslationPushItem
               {
                  Origin = BuildOrigin(projectName, pair.Value.SourceFile),
                  Locale = pair.Key,
                  Key = key,
                  Value = pair.Value.ValuesByKey.GetValueOrDefault(key)
               }
            )
         )
         .ToArray();
   }

   /// <summary>
   /// Indexes one file's values by translation key, matching keys without regard to case.
   /// The first entry for a key sets both its casing and its value.
   /// </summary>
   private static Dictionary<string, string?> IndexEntries(ResxParsedFile file)
   {
      var valuesByKey = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in file.Entries)
         valuesByKey.TryAdd(entry.Key, entry.Value);

      return valuesByKey;
   }

   /// <summary>
   /// Collects the translation keys of one origin, matching keys without regard to case.
   /// The default-locale file's casing wins; otherwise the casing of the locale file whose locale sorts first.
   /// </summary>
   private static string[] CollectKeys(Dictionary<string, IndexedFile> filesByLocale, string defaultLocale)
   {
      var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var orderedFiles = filesByLocale
         .OrderBy(pair => pair.Key == defaultLocale ? 0 : 1)
         .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase);

      foreach (var key in orderedFiles.SelectMany(static pair => pair.Value.ValuesByKey.Keys))
         keys.Add(key);

      return keys
         .OrderBy(static key => key, StringComparer.Ordinal)
         .ToArray();
   }

   internal static string BuildOrigin(string projectName, ResxSourceFile sourceFile)
   {
      var resourceSetPath = sourceFile.ResourceSetPath.Replace('\\', '/');
      return projectName + ":/" + resourceSetPath + ".resx";
   }

   /// <summary>
   /// One locale's <c>.resx</c> file with its values indexed by translation key, ignoring case.
   /// </summary>
   private sealed record IndexedFile(ResxSourceFile SourceFile, Dictionary<string, string?> ValuesByKey);
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
