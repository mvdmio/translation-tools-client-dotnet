using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// The client's translation cache. Every entry is keyed by an <see cref="EffectiveLocale"/> rather
/// than by a raw locale name, so a lookup made under the invariant culture and a lookup made under
/// the default locale share one entry instead of the empty string becoming its own bucket.
/// </summary>
internal interface ITranslationToolsClientCache
{
   TranslationToolsClientCacheEntry<TranslationItemResponse>? Get(EffectiveLocale locale, TranslationRef translation);

   TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>? GetLocale(EffectiveLocale locale);

   IReadOnlyCollection<TranslationRef> GetKnownKeys();

   ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetAsync(EffectiveLocale locale, TranslationRef translation, CancellationToken cancellationToken);

   ValueTask<TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>?> GetLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken);

   ValueTask SetAsync(EffectiveLocale locale, TranslationToolsClientCacheEntry<TranslationItemResponse> value, CancellationToken cancellationToken);

   ValueTask SetLocaleAsync(EffectiveLocale locale, TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> value, CancellationToken cancellationToken);

   ValueTask RemoveAsync(EffectiveLocale locale, TranslationRef translation, CancellationToken cancellationToken);

   ValueTask RemoveLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken);
}
