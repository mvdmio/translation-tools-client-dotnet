namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class TranslationToolsClientCacheEntry<T>
   where T : class
{
   public required T Value { get; init; }
}
