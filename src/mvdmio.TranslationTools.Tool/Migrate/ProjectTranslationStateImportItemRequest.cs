namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationStateImportItemRequest
{
   public required string Key { get; init; }
   public required IReadOnlyDictionary<string, string?> Translations { get; init; }
}
