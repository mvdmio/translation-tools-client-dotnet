using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class LocalTranslationToolsClientCache : ITranslationToolsClientCache
{
   private readonly ConcurrentDictionary<string, LocaleCacheState> _entries = new(StringComparer.Ordinal);

   public ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetAsync(string locale, TranslationRef translation, CancellationToken cancellationToken)
   {
      return ValueTask.FromResult(Get(locale, translation));
   }

   public TranslationToolsClientCacheEntry<TranslationItemResponse>? Get(string locale, TranslationRef translation)
   {
      return _entries.TryGetValue(locale, out var state) && state.Items.TryGetValue(translation, out var entry)
         ? entry
         : null;
   }

   public TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>? GetLocale(string locale)
   {
      return _entries.TryGetValue(locale, out var state)
         ? state.Locale
         : null;
   }

   public ValueTask<TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>?> GetLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      return ValueTask.FromResult(GetLocale(locale));
   }

   public ValueTask SetAsync(string locale, TranslationToolsClientCacheEntry<TranslationItemResponse> value, CancellationToken cancellationToken)
   {
      var state = _entries.GetOrAdd(locale, static _ => new LocaleCacheState());

      lock (state.SyncRoot)
      {
         var translation = new TranslationRef(value.Value.Origin, value.Value.Key);
         state.Items[translation] = value;

         if (state.Locale is not null)
            state.Locale = CreateLocaleEntry(locale, state.Items.Values.Select(static item => item.Value));
      }

      return ValueTask.CompletedTask;
   }

   public ValueTask SetLocaleAsync(string locale, TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> value, CancellationToken cancellationToken)
   {
      var state = _entries.GetOrAdd(locale, static _ => new LocaleCacheState());

      lock (state.SyncRoot)
      {
         state.Items.Clear();

         foreach (var item in value.Value.Values)
            state.Items[item.Key] = new TranslationToolsClientCacheEntry<TranslationItemResponse>
            {
               Value = new TranslationItemResponse
               {
                  Origin = item.Key.Origin,
                  Key = item.Key.Key,
                  Value = item.Value
               }
            };

         state.Locale = value;
      }

      return ValueTask.CompletedTask;
   }

   public ValueTask RemoveAsync(string locale, TranslationRef translation, CancellationToken cancellationToken)
   {
      if (!_entries.TryGetValue(locale, out var state))
         return ValueTask.CompletedTask;

      lock (state.SyncRoot)
      {
         state.Items.TryRemove(translation, out _);

         if (state.Locale is not null)
            state.Locale = CreateLocaleEntry(locale, state.Items.Values.Select(static item => item.Value));

         if (state.Items.IsEmpty && state.Locale is null)
            _entries.TryRemove(locale, out _);
      }

      return ValueTask.CompletedTask;
   }

   public ValueTask RemoveLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      _entries.TryRemove(locale, out _);
      return ValueTask.CompletedTask;
   }

   private static TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> CreateLocaleEntry(string locale, IEnumerable<TranslationItemResponse> items)
   {
      return new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>
      {
         Value = new TranslationLocaleSnapshot(
            locale,
            items.ToDictionary(static item => new TranslationRef(item.Origin, item.Key), static item => item.Value)
         )
      };
   }

   private sealed class LocaleCacheState
   {
      public object SyncRoot { get; } = new();

      public ConcurrentDictionary<TranslationRef, TranslationToolsClientCacheEntry<TranslationItemResponse>> Items { get; } = new();

      public TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>? Locale { get; set; }
   }
}
