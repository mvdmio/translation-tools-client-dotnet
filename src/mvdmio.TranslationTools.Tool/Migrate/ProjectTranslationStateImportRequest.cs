namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationStateImportRequest
{
   public required string DefaultLocale { get; init; }
   public required string[] Locales { get; init; }
   public required ProjectTranslationStateImportItemRequest[] Items { get; init; }
}
