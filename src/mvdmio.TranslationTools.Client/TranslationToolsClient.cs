using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Translation API client implementation.
/// </summary>
public sealed class TranslationToolsClient : ITranslationToolsClient, IDisposable
{
   private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
   {
      PropertyNameCaseInsensitive = true
   };

   private readonly HttpClient _client;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly ITranslationToolsClientCache _cache;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);

   private TranslationToolsClientOptions Options => _options.Value;

   /// <summary>
   /// Create a client using cache services registered in the container.
   /// </summary>
   public TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options)
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
            await RefreshLocaleAsync(locale, cancellationToken);
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

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, locale, defaultValue: null, cancellationToken);
   }

   internal async Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, CancellationToken cancellationToken = default)
   {
      var localeName = locale.Name;

      var cached = await GetCachedTranslationAsync(localeName, translation, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchTranslationAsync(localeName, translation, defaultValue, cancellationToken);
      var stored = await StoreTranslationAsync(localeName, translation, fetched, cancellationToken);
      return stored;
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

   internal async Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var localeName = locale.Name;
      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      await StoreLocaleAsync(localeName, fetched, cancellationToken);
   }

   /// <summary>
   /// Try to get a cached translation for a specific locale.
   /// </summary>
   internal TranslationItemResponse? TryGetCached(TranslationRef translation, CultureInfo locale)
   {
      return _cache.Get(locale.Name, translation)?.Value;
   }

   internal void InvalidateLocale(CultureInfo locale)
   {
      InvalidateLocaleAsync(locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   internal void Invalidate(TranslationRef translation, CultureInfo locale)
   {
      InvalidateAsync(translation, locale.Name, CancellationToken.None).GetAwaiter().GetResult();
   }

   internal Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<TranslationRef, string?> values, CancellationToken cancellationToken = default)
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

   internal Task ApplyUpdateAsync(TranslationRef translation, string? value, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return StoreTranslationUpdateAsync(
         locale.Name,
         new TranslationItemResponse
         {
            Origin = translation.Origin,
            Key = translation.Key,
            Value = value
         },
         updateLocaleCache: true,
         cancellationToken
      );
   }

   /// <inheritdoc />
   public void Dispose()
   {
      _client.Dispose();
      _initializeLock.Dispose();
   }

   private async Task<TranslationItemResponse[]> FetchLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/translations/{Uri.EscapeDataString(locale)}");
      return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse[]>(content), cancellationToken);
   }

   private async Task<TranslationItemResponse> FetchTranslationAsync(string locale, TranslationRef translation, string? defaultValue, CancellationToken cancellationToken)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(translation.Origin)}/{Uri.EscapeDataString(locale)}/{Uri.EscapeDataString(translation.Key)}";
      if (defaultValue is not null)
         url += $"?defaultValue={Uri.EscapeDataString(defaultValue)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
      return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse>(content), cancellationToken);
   }

   private async Task<T> FetchAsync<T>(HttpRequestMessage request, Func<HttpContent, Task<T?>> deserialize, CancellationToken cancellationToken) where T : class
   {
      using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      return await deserialize(response.Content) ?? throw new InvalidOperationException("Response body was empty.");
   }

   private async Task<TranslationItemResponse> StoreTranslationAsync(string locale, TranslationRef translation, TranslationItemResponse fetched, CancellationToken cancellationToken)
   {
      return await StoreTranslationUpdateAsync(locale, fetched, updateLocaleCache: true, cancellationToken);
   }

   private ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetCachedTranslationAsync(string locale, TranslationRef translation, CancellationToken cancellationToken)
   {
      return _cache.GetAsync(locale, translation, cancellationToken);
   }

   private ValueTask<TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>?> GetCachedLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      return _cache.GetLocaleAsync(locale, cancellationToken);
   }

   private async Task<TranslationLocaleSnapshot> StoreLocaleAsync(string locale, TranslationItemResponse[] fetched, CancellationToken cancellationToken)
   {
      var stored = new TranslationLocaleSnapshot(
         locale,
         fetched.ToDictionary(static item => new TranslationRef(item.Origin, item.Key), static item => item.Value)
      );

      await _cache.SetLocaleAsync(
         locale,
         new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> {
            Value = stored
         },
         cancellationToken
      );

      return stored;
   }

   private async Task<TranslationItemResponse> StoreTranslationUpdateAsync(string locale, TranslationItemResponse item, bool updateLocaleCache, CancellationToken cancellationToken)
   {
      await _cache.SetAsync(locale, new TranslationToolsClientCacheEntry<TranslationItemResponse> { Value = item }, cancellationToken);

      return item;
   }

   private async Task InvalidateLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      await _cache.RemoveLocaleAsync(locale, cancellationToken);
   }

   private async Task InvalidateAsync(TranslationRef translation, string locale, CancellationToken cancellationToken)
   {
      await _cache.RemoveAsync(locale, translation, cancellationToken);
   }

   private CultureInfo[] GetSupportedLocales()
   {
      if (Options.SupportedLocales.Length == 0)
         return [CultureInfo.CurrentUICulture];

      return Options.SupportedLocales;
   }

   private static async Task<T?> DeserializeAsync<T>(HttpContent content) where T : class
   {
      await using var stream = await content.ReadAsStreamAsync();
      return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions);
   }

}
