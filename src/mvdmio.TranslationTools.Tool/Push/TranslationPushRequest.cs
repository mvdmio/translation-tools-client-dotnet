namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class TranslationPushRequest
{
   public required TranslationPushItemRequest[] Items { get; init; }
   public bool Prune { get; init; }
}
