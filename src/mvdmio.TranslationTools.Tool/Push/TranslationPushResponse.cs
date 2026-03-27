namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class TranslationPushResponse
{
   public required int ReceivedKeyCount { get; init; }
   public required int CreatedKeyCount { get; init; }
   public required int UpdatedKeyCount { get; init; }
   public required int RemovedKeyCount { get; init; }
}
