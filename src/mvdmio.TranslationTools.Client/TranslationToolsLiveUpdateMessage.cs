namespace mvdmio.TranslationTools.Client;

internal sealed class TranslationToolsLiveUpdateMessage
{
   public string? Type { get; init; }
   public string? Origin { get; init; }
   public string? Locale { get; init; }
   public string? Key { get; init; }
   public string? Value { get; init; }
}
