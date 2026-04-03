using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Default TranslationTools API client implementation.
/// </summary>
public sealed class TranslationToolsClient : ITranslationToolsClient, IDisposable
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
   {
      PropertyNameCaseInsensitive = true
   };

   private readonly HttpClient _client;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly ITranslationToolsClientCache _cache;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);
   private readonly ConcurrentDictionary<string, ConcurrentDictionary<TranslationRef, byte>> _localeKeys = new(StringComparer.Ordinal);

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
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, CultureInfo.CurrentUICulture, cancellationToken);
   }

    public Task<TranslationItemResponse> GetAsync(string key, CancellationToken cancellationToken = default)
    {
       TranslationClientInputValidator.ValidateKey(key);
       return GetAsync(new TranslationRef("/Localizations.resx", key), cancellationToken);
    }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, locale, defaultValue: null, cancellationToken);
   }

    public Task<TranslationItemResponse> GetAsync(string key, CultureInfo locale, CancellationToken cancellationToken = default)
    {
       TranslationClientInputValidator.ValidateKey(key);
       return GetLegacyAsync(key, locale, cancellationToken);
    }

   /// <inheritdoc />
   public async Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
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
   public TranslationItemResponse? TryGetCached(TranslationRef translation)
   {
      return TryGetCached(translation, CultureInfo.CurrentUICulture);
   }

   public TranslationItemResponse? TryGetCached(string key)
   {
      return TryGetCached(new TranslationRef("/Localizations.resx", key));
   }

   /// <summary>
   /// Try to get a cached translation for a specific locale.
   /// </summary>
   public TranslationItemResponse? TryGetCached(TranslationRef translation, CultureInfo locale)
   {
      translation = ValidateTranslationRef(translation);
      return _cache.Get<TranslationItemResponse>(BuildTranslationCacheKey(locale.Name, translation))?.Value;
   }

   public TranslationItemResponse? TryGetCached(string key, CultureInfo locale)
   {
      return TryGetCached(new TranslationRef("/Localizations.resx", key), locale);
   }

   /// <inheritdoc />
   public void InvalidateLocale(CultureInfo locale)
   {
      InvalidateLocaleAsync(locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   /// <inheritdoc />
   public void Invalidate(TranslationRef translation, CultureInfo locale)
   {
      translation = ValidateTranslationRef(translation);
      InvalidateAsync(translation, locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   public void Invalidate(string key, CultureInfo locale)
   {
      Invalidate(new TranslationRef("/Localizations.resx", key), locale);
   }

   /// <inheritdoc />
   public Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<TranslationRef, string?> values, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(values);

      return StoreLocaleAsync(
         locale.Name,
          values.Select(static item => new TranslationItemResponse {
             Origin = item.Key.Origin,
            Key = item.Key.Key,
            Value = item.Value
         }).ToArray(),
         cancellationToken
      );
   }

   public Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
   {
      return ApplyLocaleUpdateAsync(
         locale,
         values.ToDictionary(x => new TranslationRef("/Localizations.resx", x.Key), x => x.Value),
         cancellationToken
      );
   }

   /// <inheritdoc />
   public Task ApplyUpdateAsync(TranslationRef translation, string? value, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      translation = ValidateTranslationRef(translation);

      return StoreTranslationUpdateAsync(
         locale.Name,
         new TranslationItemResponse {
            Origin = translation.Origin,
            Key = translation.Key,
            Value = value
         },
         updateLocaleCache: true,
         cancellationToken
      );
   }

   public Task ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return ApplyUpdateAsync(new TranslationRef(item.Origin, item.Key), item.Value, locale, cancellationToken);
   }

   internal async Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, CancellationToken cancellationToken = default)
   {
      translation = ValidateTranslationRef(translation);
      var localeName = locale.Name;

      var cached = await GetCachedTranslationAsync(localeName, translation, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchTranslationAsync(localeName, translation, defaultValue, cancellationToken);
      var stored = await StoreTranslationAsync(localeName, translation, fetched, cancellationToken);
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

    private async Task<TranslationItemResponse> FetchTranslationAsync(string locale, TranslationRef translation, string? defaultValue, CancellationToken cancellationToken)
     {
        if (string.Equals(translation.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
           return await FetchLegacyTranslationAsync(locale, translation.Key, defaultValue, cancellationToken);

        var url = $"api/v1/translations/{Uri.EscapeDataString(translation.Origin)}/{Uri.EscapeDataString(locale)}/{Uri.EscapeDataString(translation.Key)}";
        if (defaultValue is not null)
           url += $"?defaultValue={Uri.EscapeDataString(defaultValue)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
       return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse>(content), cancellationToken);
    }

    private Task<TranslationItemResponse> GetLegacyAsync(string key, CultureInfo locale, CancellationToken cancellationToken)
    {
       return GetAsync(new TranslationRef("/Localizations.resx", key), locale, defaultValue: null, cancellationToken);
    }

    private async Task<TranslationItemResponse> FetchLegacyTranslationAsync(string locale, string key, string? defaultValue, CancellationToken cancellationToken)
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

    private async Task<TranslationItemResponse> StoreTranslationAsync(string locale, TranslationRef translation, TranslationItemResponse fetched, CancellationToken cancellationToken)
    {
       fetched = new TranslationItemResponse {
          Origin = translation.Origin,
          Key = translation.Key,
          Value = fetched.Value
       };

      return await StoreTranslationUpdateAsync(locale, fetched, updateLocaleCache: true, cancellationToken);
   }

    private ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetCachedTranslationAsync(string locale, TranslationRef translation, CancellationToken cancellationToken)
    {
       return _cache.GetAsync<TranslationItemResponse>(BuildTranslationCacheKey(locale, translation), cancellationToken);
    }

    private ValueTask<TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>?> GetCachedLocaleAsync(string locale, CancellationToken cancellationToken)
    {
       return _cache.GetAsync<TranslationLocaleSnapshot>(BuildLocaleCacheKey(locale), cancellationToken);
    }

    private async Task<TranslationLocaleSnapshot> StoreLocaleAsync(string locale, TranslationItemResponse[] fetched, CancellationToken cancellationToken)
    {
       var stored = TranslationLocaleSnapshot.FromItems(locale, fetched);
       await ReplaceLocaleItemsAsync(locale, stored.Items, cancellationToken);
       await _cache.SetAsync(
          BuildLocaleCacheKey(locale),
          new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> {
             Value = stored
          },
          cancellationToken
      );

      return stored;
   }

     private async Task ReplaceLocaleItemsAsync(string locale, IReadOnlyCollection<TranslationItemResponse> items, CancellationToken cancellationToken)
     {
        var localeKeyIndex = _localeKeys.GetOrAdd(locale, static _ => new ConcurrentDictionary<TranslationRef, byte>());
        var nextKeys = new HashSet<TranslationRef>();

        foreach (var item in items)
        {
          var translation = NormalizeTranslationItem(item);
          var translationRef = new TranslationRef(translation.Origin, translation.Key);

          await _cache.SetAsync(
             BuildTranslationCacheKey(locale, translationRef),
             new TranslationToolsClientCacheEntry<TranslationItemResponse> {
                Value = translation
             },
             cancellationToken
          );

          localeKeyIndex[translationRef] = 0;
          nextKeys.Add(translationRef);
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
        item = NormalizeTranslationItem(item);
        var translationRef = new TranslationRef(item.Origin, item.Key);

        await _cache.SetAsync(
           BuildTranslationCacheKey(locale, translationRef),
           new TranslationToolsClientCacheEntry<TranslationItemResponse> {
              Value = item
           },
           cancellationToken
        );

        _localeKeys.GetOrAdd(locale, static _ => new ConcurrentDictionary<TranslationRef, byte>())[translationRef] = 0;

      if (updateLocaleCache)
         await UpdateLocaleCacheEntryAsync(locale, item, cancellationToken);

      return item;
   }

    private async Task UpdateLocaleCacheEntryAsync(string locale, TranslationItemResponse item, CancellationToken cancellationToken)
    {
        var cachedLocale = await GetCachedLocaleAsync(locale, cancellationToken);
        if (cachedLocale is null)
           return;

        var updated = cachedLocale.Value.Items
           .Where(existing => !string.Equals(existing.Origin, item.Origin, StringComparison.OrdinalIgnoreCase) || !string.Equals(existing.Key, item.Key, StringComparison.Ordinal))
           .Append(item)
           .ToArray();

        await _cache.SetAsync(
           BuildLocaleCacheKey(locale),
           new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> {
              Value = TranslationLocaleSnapshot.FromItems(locale, updated)
           },
           cancellationToken
        );
    }

   private async Task InvalidateLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      await _cache.RemoveAsync(BuildLocaleCacheKey(locale), cancellationToken);

      if (!_localeKeys.TryRemove(locale, out var localeKeyIndex))
         return;

        foreach (var translation in localeKeyIndex.Keys)
           await _cache.RemoveAsync(BuildTranslationCacheKey(locale, translation), cancellationToken);
     }

     private async Task InvalidateAsync(TranslationRef translation, string locale, CancellationToken cancellationToken)
     {
        await _cache.RemoveAsync(BuildTranslationCacheKey(locale, translation), cancellationToken);

        if (_localeKeys.TryGetValue(locale, out var localeKeyIndex))
        {
           localeKeyIndex.TryRemove(translation, out _);

         if (localeKeyIndex.IsEmpty)
            _localeKeys.TryRemove(locale, out _);
      }

        var cachedLocale = await GetCachedLocaleAsync(locale, cancellationToken);
        if (cachedLocale is null || !cachedLocale.Value.Contains(translation))
           return;

        var updated = cachedLocale.Value.Items
           .Where(existing => !string.Equals(existing.Origin, translation.Origin, StringComparison.OrdinalIgnoreCase) || !string.Equals(existing.Key, translation.Key, StringComparison.Ordinal))
           .ToArray();

        await _cache.SetAsync(
           BuildLocaleCacheKey(locale),
           new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> {
              Value = TranslationLocaleSnapshot.FromItems(locale, updated)
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

    private static string BuildTranslationCacheKey(string locale, TranslationRef translation)
     {
        if (string.Equals(translation.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
           return $"translationtools:item:{locale}:{translation.Key}";

        return $"translationtools:item:{locale}:{translation.Origin.ToLowerInvariant()}:{translation.Key}";
     }

    private static string BuildLocaleCacheKey(string locale)
    {
       return $"translationtools:locale:{locale}";
    }

    private static TranslationRef ValidateTranslationRef(TranslationRef translation)
    {
       return new TranslationRef(translation.Origin, translation.Key);
    }

    private static TranslationItemResponse NormalizeTranslationItem(TranslationItemResponse item)
    {
       return new TranslationItemResponse {
          Origin = TranslationClientInputValidator.ValidateOrigin(item.Origin),
          Key = TranslationClientInputValidator.ValidateKey(item.Key),
          Value = item.Value
       };
    }
}
