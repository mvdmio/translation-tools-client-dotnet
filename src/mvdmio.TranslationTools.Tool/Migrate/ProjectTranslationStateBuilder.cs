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
      var shouldPrefixKeys = parsedFiles
         .Select(static x => x.SourceFile.ResourceSetName)
         .Distinct(StringComparer.Ordinal)
         .Skip(1)
         .Any();
      var locales = parsedFiles
         .Select(x => x.SourceFile.Locale ?? normalizedDefaultLocale)
         .Distinct(StringComparer.Ordinal)
         .OrderBy(static x => x, StringComparer.Ordinal)
         .ToArray();

      var warnings = parsedFiles
         .Where(static x => x.Entries.Count == 0)
         .Select(x => $"No importable strings found in '{x.SourceFile.RelativePath}'. Locale '{x.SourceFile.Locale ?? normalizedDefaultLocale}' kept in locale metadata.")
         .ToArray();

      var collectedKeys = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
      var keyOrigins = new Dictionary<string, string>(StringComparer.Ordinal);

      foreach (var parsedFile in parsedFiles)
      {
         var locale = parsedFile.SourceFile.Locale ?? normalizedDefaultLocale;

         foreach (var entry in parsedFile.Entries)
         {
            var effectiveKey = shouldPrefixKeys
               ? $"{parsedFile.SourceFile.ResourceSetName}.{entry.Key}"
               : entry.Key;
            var origin = $"{parsedFile.SourceFile.ResourceSetName}::{entry.Key}";

            if (keyOrigins.TryGetValue(effectiveKey, out var existingOrigin)
                && !string.Equals(existingOrigin, origin, StringComparison.Ordinal))
               throw new InvalidOperationException($"Duplicate effective API key '{effectiveKey}' produced by '{existingOrigin}' and '{origin}'.");

            keyOrigins[effectiveKey] = origin;

            if (!collectedKeys.TryGetValue(effectiveKey, out var translations))
            {
               translations = new Dictionary<string, string?>(StringComparer.Ordinal);
               collectedKeys[effectiveKey] = translations;
            }

            translations[locale] = entry.Value;
         }
      }

      var items = collectedKeys
         .OrderBy(static x => x.Key, StringComparer.Ordinal)
         .Select(
            x => new ProjectTranslationStateItem {
               Key = x.Key,
               Translations = locales.ToDictionary(locale => locale, locale => x.Value.TryGetValue(locale, out var value) ? value : null, StringComparer.Ordinal)
            }
         )
         .ToArray();

      var localeValueCounts = locales.ToDictionary(
         locale => locale,
         locale => items.Count(item => item.Translations[locale] is not null),
         StringComparer.Ordinal
      );

      return (
         new ProjectTranslationState {
            DefaultLocale = normalizedDefaultLocale,
            Locales = locales,
            Items = items
         },
         new MigrationReport {
            SourceFiles = parsedFiles.Select(static x => x.SourceFile.RelativePath).ToArray(),
            Warnings = warnings,
            DefaultLocale = normalizedDefaultLocale,
            Locales = locales,
            LocaleValueCounts = localeValueCounts,
            KeyCount = items.Length
         }
      );
   }
}
