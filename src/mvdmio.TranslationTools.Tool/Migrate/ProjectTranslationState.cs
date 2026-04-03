namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationState
{
   public required string DefaultLocale { get; init; }
   public required IReadOnlyCollection<string> Locales { get; init; }
   public required IReadOnlyCollection<ProjectTranslationStateItem> Items { get; init; }
}

internal sealed class ProjectTranslationStateItem
{
   public required string Origin { get; init; }
   public required string Key { get; init; }
   public required IReadOnlyDictionary<string, string?> Translations { get; init; }
}
