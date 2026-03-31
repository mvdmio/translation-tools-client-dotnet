using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class LocalTranslationToolsClientCache : ITranslationToolsClientCache
{
   private readonly ConcurrentDictionary<string, object> _entries = new();

   public ValueTask<TranslationToolsClientCacheEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
      where T : class
   {
      return ValueTask.FromResult(Get<T>(key));
   }

   public TranslationToolsClientCacheEntry<T>? Get<T>(string key)
      where T : class
   {
      return _entries.TryGetValue(key, out var entry)
         ? entry as TranslationToolsClientCacheEntry<T>
         : null;
   }

   public ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
      where T : class
   {
      _entries[key] = value;

      return ValueTask.CompletedTask;
   }
}
