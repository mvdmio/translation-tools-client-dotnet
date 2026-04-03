namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class TranslationPushItemRequest
{
   public required string Origin { get; init; }
   public required string Locale { get; init; }
   public required string Key { get; init; }
   public string? Value { get; init; }
}
