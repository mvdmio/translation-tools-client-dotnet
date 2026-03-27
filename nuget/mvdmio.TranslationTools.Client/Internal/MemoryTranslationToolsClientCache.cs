using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class MemoryTranslationToolsClientCache : ITranslationToolsClientCache
{
   private readonly IMemoryCache _cache;
   private readonly TimeSpan _cacheDuration;

   public MemoryTranslationToolsClientCache(IMemoryCache cache, TimeSpan cacheDuration)
   {
      _cache = cache;
      _cacheDuration = cacheDuration;
   }

   public ValueTask<TranslationToolsClientCacheEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
      where T : class
   {
      return ValueTask.FromResult(Get<T>(key));
   }

   public TranslationToolsClientCacheEntry<T>? Get<T>(string key)
      where T : class
   {
      if (!_cache.TryGetValue(key, out TranslationToolsClientCacheEntry<T>? entry) || entry is null)
         return null;

      return entry;
   }

   public ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
      where T : class
   {
      _cache.Set(
         key,
         value,
         new MemoryCacheEntryOptions {
            AbsoluteExpirationRelativeToNow = _cacheDuration
         }
      );

      return ValueTask.CompletedTask;
   }
}
