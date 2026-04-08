using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal interface ITranslationToolsClientCache
{
   TranslationToolsClientCacheEntry<TranslationItemResponse>? Get(string locale, TranslationRef translation);

   TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>? GetLocale(string locale);

   ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetAsync(string locale, TranslationRef translation, CancellationToken cancellationToken);

   ValueTask<TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>?> GetLocaleAsync(string locale, CancellationToken cancellationToken);

   ValueTask SetAsync(string locale, TranslationToolsClientCacheEntry<TranslationItemResponse> value, CancellationToken cancellationToken);

   ValueTask SetLocaleAsync(string locale, TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> value, CancellationToken cancellationToken);

   ValueTask RemoveAsync(string locale, TranslationRef translation, CancellationToken cancellationToken);

   ValueTask RemoveLocaleAsync(string locale, CancellationToken cancellationToken);
}
