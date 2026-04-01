using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Default TranslationTools API client implementation.
/// </summary>
public sealed class TranslationToolsClient : ITranslationToolsClient, IDisposable
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
      PropertyNameCaseInsensitive = true
   };

   private readonly HttpClient _client;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly ITranslationToolsClientCache _cache;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);
   private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _localeKeys = new(StringComparer.Ordinal);

   private TranslationToolsClientOptions Options => _options.Value;

   /// <summary>
   /// Create a client using cache services registered in the container.
   /// </summary>
   public TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options, IServiceProvider serviceProvider)
      : this(client, options, new LocalTranslationToolsClientCache())
   {
   }

   internal TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options, ITranslationToolsClientCache cache)
   {
      _client = client;
      _options = options;
      _cache = cache;

      if (string.IsNullOrWhiteSpace(Options.ApiKey))
         throw new ArgumentException("ApiKey is required.", nameof(options));

      _client.BaseAddress = new Uri(TranslationToolsClientOptions.DEFAULT_BASE_URL);
      _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Options.ApiKey);
   }

   /// <inheritdoc />
   public async Task Initialize(CancellationToken cancellationToken = default)
   {
      await _initializeLock.WaitAsync(cancellationToken);

      try
      {
         foreach (var locale in GetSupportedLocales())
            await InitializeLocaleAsync(locale, cancellationToken);
      }
      finally
      {
         _initializeLock.Release();
      }
   }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(string key, CancellationToken cancellationToken = default)
   {
      return GetAsync(key, CultureInfo.CurrentUICulture, cancellationToken);
   }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(string key, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return GetAsync(key, locale, defaultValue: null, cancellationToken);
   }

   /// <inheritdoc />
   public async Task<IReadOnlyDictionary<string, string?>> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var localeName = locale.Name;
      var cached = await GetCachedLocaleAsync(localeName, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      return await StoreLocaleAsync(localeName, fetched, cancellationToken);
   }

   /// <inheritdoc />
   public async Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var localeName = locale.Name;
      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      await StoreLocaleAsync(localeName, fetched, cancellationToken);
   }

   /// <summary>
   /// Try to get a cached translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public TranslationItemResponse? TryGetCached(string key)
   {
      return TryGetCached(key, CultureInfo.CurrentUICulture);
   }

   /// <summary>
   /// Try to get a cached translation for a specific locale.
   /// </summary>
   public TranslationItemResponse? TryGetCached(string key, CultureInfo locale)
   {
      key = TranslationClientInputValidator.ValidateKey(key);
      return _cache.Get<TranslationItemResponse>(BuildTranslationCacheKey(locale.Name, key))?.Value;
   }

   /// <inheritdoc />
   public void InvalidateLocale(CultureInfo locale)
   {
      InvalidateLocaleAsync(locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   /// <inheritdoc />
   public void Invalidate(string key, CultureInfo locale)
   {
      key = TranslationClientInputValidator.ValidateKey(key);
      InvalidateAsync(key, locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   /// <inheritdoc />
   public Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(values);

      return StoreLocaleAsync(
         locale.Name,
         values.Select(static item => new TranslationItemResponse {
            Key = item.Key,
            Value = item.Value
         }).ToArray(),
         cancellationToken
      );
   }

   /// <inheritdoc />
   public Task ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(item);
      item = new TranslationItemResponse {
         Key = TranslationClientInputValidator.ValidateKey(item.Key),
         Value = item.Value
      };

      return StoreTranslationUpdateAsync(locale.Name, item, updateLocaleCache: true, cancellationToken);
   }

   internal async Task<TranslationItemResponse> GetAsync(string key, CultureInfo locale, string? defaultValue, CancellationToken cancellationToken = default)
   {
      key = TranslationClientInputValidator.ValidateKey(key);
      var localeName = locale.Name;

      var cached = await GetCachedTranslationAsync(localeName, key, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchTranslationAsync(localeName, key, defaultValue, cancellationToken);
      var stored = await StoreTranslationAsync(localeName, key, fetched, cancellationToken);
      return stored;
   }

   /// <inheritdoc />
   public void Dispose()
   {
      _client.Dispose();
      _initializeLock.Dispose();
   }

    private Task InitializeLocaleAsync(CultureInfo locale, CancellationToken cancellationToken)
    {
       return RefreshLocaleAsync(locale, cancellationToken);
    }

   private async Task<TranslationItemResponse[]> FetchLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/translations/{Uri.EscapeDataString(locale)}");
      return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse[]>(content), cancellationToken);
   }

   private async Task<TranslationItemResponse> FetchTranslationAsync(string locale, string key, string? defaultValue, CancellationToken cancellationToken)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(locale)}/{Uri.EscapeDataString(key)}";
      if (defaultValue is not null)
         url += $"?defaultValue={Uri.EscapeDataString(defaultValue)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
      return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse>(content), cancellationToken);
   }

   private async Task<T> FetchAsync<T>(HttpRequestMessage request, Func<HttpContent, Task<T?>> deserialize, CancellationToken cancellationToken)
      where T : class
   {
      using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      return await deserialize(response.Content) ?? throw new InvalidOperationException("Response body was empty.");
   }

   private async Task<TranslationItemResponse> StoreTranslationAsync(string locale, string key, TranslationItemResponse fetched, CancellationToken cancellationToken)
   {
      fetched = new TranslationItemResponse {
         Key = key,
         Value = fetched.Value
      };

      return await StoreTranslationUpdateAsync(locale, fetched, updateLocaleCache: true, cancellationToken);
   }

   private ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetCachedTranslationAsync(string locale, string key, CancellationToken cancellationToken)
   {
      return _cache.GetAsync<TranslationItemResponse>(BuildTranslationCacheKey(locale, key), cancellationToken);
   }

   private ValueTask<TranslationToolsClientCacheEntry<Dictionary<string, string?>>?> GetCachedLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      return _cache.GetAsync<Dictionary<string, string?>>(BuildLocaleCacheKey(locale), cancellationToken);
   }

   private async Task<Dictionary<string, string?>> StoreLocaleAsync(string locale, TranslationItemResponse[] fetched, CancellationToken cancellationToken)
   {
      var stored = ToLocaleDictionary(fetched);
      await ReplaceLocaleItemsAsync(locale, stored, cancellationToken);
      await _cache.SetAsync(
         BuildLocaleCacheKey(locale),
         new TranslationToolsClientCacheEntry<Dictionary<string, string?>> {
            Value = stored
         },
         cancellationToken
      );

      return stored;
   }

    private async Task ReplaceLocaleItemsAsync(string locale, IReadOnlyDictionary<string, string?> items, CancellationToken cancellationToken)
    {
       var localeKeyIndex = _localeKeys.GetOrAdd(locale, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
       var nextKeys = new HashSet<string>(StringComparer.Ordinal);

       foreach (var (key, value) in items)
       {
          var validatedKey = TranslationClientInputValidator.ValidateKey(key);
          var translation = new TranslationItemResponse {
             Key = validatedKey,
             Value = value
          };

          await _cache.SetAsync(
             BuildTranslationCacheKey(locale, validatedKey),
             new TranslationToolsClientCacheEntry<TranslationItemResponse> {
                Value = translation
             },
             cancellationToken
          );

          localeKeyIndex[validatedKey] = 0;
          nextKeys.Add(validatedKey);
       }

       foreach (var staleKey in localeKeyIndex.Keys.Where(key => !nextKeys.Contains(key)).ToArray())
       {
          await _cache.RemoveAsync(BuildTranslationCacheKey(locale, staleKey), cancellationToken);
          localeKeyIndex.TryRemove(staleKey, out _);
       }

       if (localeKeyIndex.IsEmpty)
          _localeKeys.TryRemove(locale, out _);
    }

    private async Task<TranslationItemResponse> StoreTranslationUpdateAsync(string locale, TranslationItemResponse item, bool updateLocaleCache, CancellationToken cancellationToken)
    {
       await _cache.SetAsync(
          BuildTranslationCacheKey(locale, item.Key),
          new TranslationToolsClientCacheEntry<TranslationItemResponse> {
             Value = item
          },
          cancellationToken
       );

       _localeKeys.GetOrAdd(locale, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[item.Key] = 0;

       if (updateLocaleCache)
          await UpdateLocaleCacheEntryAsync(locale, item, cancellationToken);

       return item;
    }

    private async Task UpdateLocaleCacheEntryAsync(string locale, TranslationItemResponse item, CancellationToken cancellationToken)
    {
       var cachedLocale = await GetCachedLocaleAsync(locale, cancellationToken);
       if (cachedLocale is null)
          return;

       var updated = new Dictionary<string, string?>(cachedLocale.Value, StringComparer.Ordinal) {
          [item.Key] = item.Value
       };

       await _cache.SetAsync(
          BuildLocaleCacheKey(locale),
          new TranslationToolsClientCacheEntry<Dictionary<string, string?>> {
             Value = updated
          },
          cancellationToken
       );
    }

    private async Task InvalidateLocaleAsync(string locale, CancellationToken cancellationToken)
    {
       await _cache.RemoveAsync(BuildLocaleCacheKey(locale), cancellationToken);

       if (!_localeKeys.TryRemove(locale, out var localeKeyIndex))
          return;

       foreach (var key in localeKeyIndex.Keys)
          await _cache.RemoveAsync(BuildTranslationCacheKey(locale, key), cancellationToken);
    }

    private async Task InvalidateAsync(string key, string locale, CancellationToken cancellationToken)
    {
       await _cache.RemoveAsync(BuildTranslationCacheKey(locale, key), cancellationToken);

       if (_localeKeys.TryGetValue(locale, out var localeKeyIndex))
       {
          localeKeyIndex.TryRemove(key, out _);

          if (localeKeyIndex.IsEmpty)
             _localeKeys.TryRemove(locale, out _);
       }

       var cachedLocale = await GetCachedLocaleAsync(locale, cancellationToken);
       if (cachedLocale is null || !cachedLocale.Value.ContainsKey(key))
          return;

       var updated = new Dictionary<string, string?>(cachedLocale.Value, StringComparer.Ordinal);
       updated.Remove(key);

       await _cache.SetAsync(
          BuildLocaleCacheKey(locale),
          new TranslationToolsClientCacheEntry<Dictionary<string, string?>> {
             Value = updated
          },
          cancellationToken
       );
    }

   private CultureInfo[] GetSupportedLocales()
   {
      if (Options.SupportedLocales.Length == 0)
         return [CultureInfo.CurrentUICulture];

      return Options.SupportedLocales
         .Where(x => !string.IsNullOrWhiteSpace(x.Name))
         .GroupBy(x => x.Name, StringComparer.Ordinal)
         .Select(x => x.First())
         .ToArray();
   }

   private static async Task<T?> DeserializeAsync<T>(HttpContent content)
      where T : class
   {
      await using var stream = await content.ReadAsStreamAsync();
      return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions);
   }

   private static string BuildTranslationCacheKey(string locale, string key)
   {
      return $"translationtools:item:{locale}:{key}";
   }

   private static Dictionary<string, string?> ToLocaleDictionary(IEnumerable<TranslationItemResponse> items)
   {
      var result = new Dictionary<string, string?>(StringComparer.Ordinal);

      foreach (var item in items)
         result[item.Key] = item.Value;

      return result;
   }

   private static string BuildLocaleCacheKey(string locale)
   {
      return $"translationtools:locale:{locale}";
   }
}
