namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationState
{
   public required string DefaultLocale { get; init; }
   public required IReadOnlyCollection<string> Locales { get; init; }
   public required IReadOnlyCollection<ProjectTranslationStateItem> Items { get; init; }
}
