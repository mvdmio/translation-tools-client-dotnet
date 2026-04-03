namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ProjectTranslationStateImportItemRequest
{
   public required string Origin { get; init; }
   public required string Locale { get; init; }
   public required string Key { get; init; }
   public required string? Value { get; init; }
   public IReadOnlyDictionary<string, string?> Translations => new Dictionary<string, string?> { [Locale] = Value };
}
