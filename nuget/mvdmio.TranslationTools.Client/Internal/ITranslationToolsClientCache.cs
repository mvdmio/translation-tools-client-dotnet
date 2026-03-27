using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal interface ITranslationToolsClientCache
{
   TranslationToolsClientCacheEntry<T>? Get<T>(string key)
      where T : class;

   ValueTask<TranslationToolsClientCacheEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
      where T : class;

   ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
      where T : class;
}
