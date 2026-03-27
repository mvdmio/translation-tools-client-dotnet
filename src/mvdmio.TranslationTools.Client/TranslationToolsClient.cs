using System;
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

   private TranslationToolsClientOptions Options => _options.Value;

   /// <summary>
   /// Create a client using cache services registered in the container.
   /// </summary>
   public TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options, IServiceProvider serviceProvider)
      : this(client, options, TranslationToolsClientCacheFactory.Create(serviceProvider))
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

   private async Task InitializeLocaleAsync(CultureInfo locale, CancellationToken cancellationToken)
   {
      var localeName = locale.Name;
      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      await CacheLocaleItemsAsync(localeName, fetched, cancellationToken);
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
      var stored = new TranslationToolsClientCacheEntry<TranslationItemResponse> {
         Value = fetched
      };

      await _cache.SetAsync(BuildTranslationCacheKey(locale, key), stored, cancellationToken);
      return stored.Value;
   }

   private ValueTask<TranslationToolsClientCacheEntry<TranslationItemResponse>?> GetCachedTranslationAsync(string locale, string key, CancellationToken cancellationToken)
   {
      return _cache.GetAsync<TranslationItemResponse>(BuildTranslationCacheKey(locale, key), cancellationToken);
   }

   private async ValueTask CacheLocaleItemsAsync(string locale, TranslationItemResponse[] items, CancellationToken cancellationToken)
   {
      foreach (var item in items)
      {
         await _cache.SetAsync(
            BuildTranslationCacheKey(locale, item.Key),
            new TranslationToolsClientCacheEntry<TranslationItemResponse> {
               Value = item
            },
            cancellationToken
         );
      }
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
}
