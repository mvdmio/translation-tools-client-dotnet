#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class HybridTranslationToolsClientCache : ITranslationToolsClientCache
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HybridCache _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;
    private readonly ConcurrentDictionary<string, byte> _knownKeys = new();
    private readonly ConcurrentDictionary<string, string> _entries = new();

   public HybridTranslationToolsClientCache(HybridCache cache, TimeSpan cacheDuration)
   {
      _cache = cache;
      _cacheOptions = new HybridCacheEntryOptions {
         Expiration = cacheDuration,
         LocalCacheExpiration = cacheDuration
      };
   }

   public async ValueTask<TranslationToolsClientCacheEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken)
      where T : class
   {
      if (!_knownKeys.ContainsKey(key))
         return null;

      var json = await _cache.GetOrCreateAsync(
         key,
         static _ => new ValueTask<string?>(result: null),
         _cacheOptions,
         cancellationToken: cancellationToken
      );

      if (string.IsNullOrWhiteSpace(json))
      {
         _knownKeys.TryRemove(key, out _);
         return null;
      }

       var entry = JsonSerializer.Deserialize<TranslationToolsClientCacheEntry<T>>(json, SerializerOptions);
       return entry;
    }

   public TranslationToolsClientCacheEntry<T>? Get<T>(string key)
      where T : class
   {
      if (!_entries.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json))
         return null;

      return JsonSerializer.Deserialize<TranslationToolsClientCacheEntry<T>>(json, SerializerOptions);
   }

    public async ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
       where T : class
    {
       var json = JsonSerializer.Serialize(value, SerializerOptions);

       await _cache.SetAsync(key, json, _cacheOptions, cancellationToken: cancellationToken);
       _knownKeys[key] = 0;
       _entries[key] = json;
    }
}
#endif
