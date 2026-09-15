namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class TranslationPushRequest
{
   public required TranslationPushItemRequest[] Items { get; init; }

   /// <summary>Self-declared deployment environment name; null/blank declares into the unnamed environment.</summary>
   public string? Environment { get; init; }

   public bool Prune { get; init; }
}
