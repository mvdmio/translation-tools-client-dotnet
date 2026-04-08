namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationStateBuilder
{
   private readonly ResxResourceSetParser _parser;

   public ProjectTranslationStateBuilder()
      : this(new ResxResourceSetParser())
   {
   }

   internal ProjectTranslationStateBuilder(ResxResourceSetParser parser)
   {
      _parser = parser;
   }

   public (ProjectTranslationState State, MigrationReport Report) Build(ResxMigrationScanResult scanResult, string? defaultLocale)
   {
      if (string.IsNullOrWhiteSpace(defaultLocale))
         throw new InvalidOperationException("Could not resolve default locale. Add defaultLocale to .mvdmio-translations.yml or configure a remote project default locale.");

      var normalizedDefaultLocale = ResxMigrationScanner.NormalizeLocale(defaultLocale);
      var parsedFiles = scanResult.SourceFiles.Select(_parser.Parse).ToArray();
      var locales = parsedFiles
          .Select(x => x.SourceFile.Locale ?? normalizedDefaultLocale)
          .Distinct(StringComparer.Ordinal)
         .OrderBy(static x => x, StringComparer.Ordinal)
         .ToArray();

      var warnings = parsedFiles
         .Where(static x => x.Entries.Count == 0)
         .Select(x => $"No importable strings found in '{x.SourceFile.RelativePath}'. Locale '{x.SourceFile.Locale ?? normalizedDefaultLocale}' kept in locale metadata.")
         .ToArray();

      var shouldPrefixKeys = parsedFiles.Select(static x => x.SourceFile.ResourceSetName).Distinct(StringComparer.Ordinal).Count() > 1;

      var items = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
      var origins = new Dictionary<string, string>(StringComparer.Ordinal);

      foreach (var parsedFile in parsedFiles)
      {
         var locale = parsedFile.SourceFile.Locale ?? normalizedDefaultLocale;
         var origin = Push.ProjectManifestScanner.BuildOrigin(parsedFile.SourceFile);

         foreach (var entry in parsedFile.Entries)
         {
            var effectiveKey = shouldPrefixKeys ? parsedFile.SourceFile.ResourceSetName + "." + entry.Key : entry.Key;
            var identity = origin + "::" + effectiveKey;
            origins[identity] = origin;

            if (!items.TryGetValue(identity, out var translations))
            {
               translations = new Dictionary<string, string?>(StringComparer.Ordinal);
               items[identity] = translations;
            }

            foreach (var knownLocale in locales)
               translations.TryAdd(knownLocale, null);

            translations[locale] = entry.Value;
         }
      }

      var localeValueCounts = locales.ToDictionary(
          locale => locale,
          locale => items.Values.Count(item => item.TryGetValue(locale, out var value) && value is not null),
          StringComparer.Ordinal
      );

      var resultItems = items.OrderBy(static x => x.Key, StringComparer.Ordinal)
         .Select(x => new ProjectTranslationStateItem
         {
            Origin = origins[x.Key],
            Key = x.Key[(origins[x.Key].Length + 2)..],
            Translations = x.Value
         })
         .ToArray();

      return (
         new ProjectTranslationState
         {
            DefaultLocale = normalizedDefaultLocale,
            Locales = locales,
            Items = resultItems
         },
         new MigrationReport
         {
            SourceFiles = parsedFiles.Select(static x => x.SourceFile.RelativePath).ToArray(),
            Warnings = warnings,
            DefaultLocale = normalizedDefaultLocale,
            Locales = locales,
            LocaleValueCounts = localeValueCounts,
            KeyCount = resultItems.Length
         }
       );
   }
}
