using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class LocalTranslationToolsClientCache : ITranslationToolsClientCache
{
   private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
   private readonly TimeSpan _cacheDuration;

   public LocalTranslationToolsClientCache(TimeSpan cacheDuration)
   {
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
      if (!_entries.TryGetValue(key, out var entry))
         return null;

      if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
      {
         _entries.TryRemove(key, out _);
         return null;
      }

      return entry.Value as TranslationToolsClientCacheEntry<T>;
   }

   public ValueTask SetAsync<T>(string key, TranslationToolsClientCacheEntry<T> value, CancellationToken cancellationToken)
      where T : class
   {
      _entries[key] = new CacheEntry {
         Value = value,
         ExpiresAt = DateTimeOffset.UtcNow.Add(_cacheDuration)
      };

      return ValueTask.CompletedTask;
   }

   private sealed class CacheEntry
   {
      public required object Value { get; init; }
      public required DateTimeOffset ExpiresAt { get; init; }
   }
}
