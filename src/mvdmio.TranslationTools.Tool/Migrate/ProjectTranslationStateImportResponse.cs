namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationStateImportResponse
{
   public required int ReceivedKeyCount { get; init; }
   public required int ReceivedLocaleCount { get; init; }
   public required int CreatedTranslationCount { get; init; }
   public required int UpdatedTranslationCount { get; init; }
}
