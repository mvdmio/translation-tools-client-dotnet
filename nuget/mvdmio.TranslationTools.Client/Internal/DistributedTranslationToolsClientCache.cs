using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class DistributedTranslationToolsClientCache : ITranslationToolsClientCache
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

   private readonly IDistributedCache _cache;
   private readonly DistributedCacheEntryOptions _cacheOptions;

   public DistributedTranslationToolsClientCache(IDistributedCache cache, TimeSpan cacheDuration)
   {
      _cache = cache;
      _cacheOptions = new DistributedCacheEntryOptions {
         AbsoluteExpirationRelativeToNow = cacheDuration
      };
   }

   public async ValueTask<TranslationToolsClientCacheEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
      where T : class
   {
      return Deserialize<T>(await _cache.GetStringAsync(key, cancellationToken));
   }

   public TranslationToolsClientCacheEntry<T>? Get<T>(string key)
      where T : class
   {
      return Deserialize<T>(_cache.GetString(key));
   }

   public async ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
      where T : class
   {
      var json = JsonSerializer.Serialize(value, SerializerOptions);

      await _cache.SetStringAsync(key, json, _cacheOptions, cancellationToken);
   }

   private static TranslationToolsClientCacheEntry<T>? Deserialize<T>(string? json)
      where T : class
   {
      if (string.IsNullOrWhiteSpace(json))
         return null;

      return JsonSerializer.Deserialize<TranslationToolsClientCacheEntry<T>>(json, SerializerOptions);
   }
}
