namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class TranslationPushItemRequest
{
   public required string Key { get; init; }
   public string? DefaultValue { get; init; }
}
