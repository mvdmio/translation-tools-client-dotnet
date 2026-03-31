namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class MigrationReport
{
   public required IReadOnlyCollection<string> SourceFiles { get; init; }
   public required IReadOnlyCollection<string> Warnings { get; init; }
   public required string DefaultLocale { get; init; }
   public required IReadOnlyCollection<string> Locales { get; init; }
   public required IReadOnlyDictionary<string, int> LocaleValueCounts { get; init; }
   public required int KeyCount { get; init; }
}
