using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
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

   private const string PlatformName = "dotnet";

   private static readonly string _clientVersion = ResolveClientVersion();

   private readonly HttpClient _client;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly ITranslationToolsClientCache _cache;
   private readonly TimeProvider _timeProvider;
   private readonly ILogger? _logger;
   private readonly Guid _clientId;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);

   private CancellationTokenSource? _heartbeatCts;
   private int _heartbeatStarted;

   private TranslationToolsClientOptions Options => _options.Value;

   private Uri BaseUri => new(Options.BaseUrlOverride);

   /// <summary>
   /// Create a client using cache services registered in the container.
   /// </summary>
   public TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options, ILogger<TranslationToolsClient>? logger = null)
      : this(client, options, new LocalTranslationToolsClientCache(), logger: logger)
   {
   }

   internal TranslationToolsClient(
      HttpClient client,
      IOptions<TranslationToolsClientOptions> options,
      ITranslationToolsClientCache cache,
      TimeProvider? timeProvider = null,
      IClientIdStore? clientIdStore = null,
      ILogger? logger = null)
   {
      _client = client;
      _options = options;
      _cache = cache;
      _timeProvider = timeProvider ?? TimeProvider.System;
      _logger = logger;
      _clientId = (clientIdStore ?? new FileClientIdStore()).GetOrCreateClientId();

      if (string.IsNullOrWhiteSpace(Options.ApiKey))
         throw new ArgumentException("ApiKey is required.", nameof(options));

      if (string.IsNullOrWhiteSpace(Options.DefaultLocale))
         throw new ArgumentException("DefaultLocale is required.", nameof(options));

      try
      {
         _ = new CultureInfo(Options.DefaultLocale);
      }
      catch (CultureNotFoundException exception)
      {
         throw new ArgumentException($"DefaultLocale '{Options.DefaultLocale}' is not a recognised locale.", nameof(options), exception);
      }

      _client.BaseAddress = BaseUri;
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

      StartHeartbeat();
   }

   private void StartHeartbeat()
   {
      if (!Options.EnableHeartbeat)
         return;

      if (Interlocked.CompareExchange(ref _heartbeatStarted, 1, 0) != 0)
         return;

      _heartbeatCts = new CancellationTokenSource();
      var token = _heartbeatCts.Token;
      _ = Task.Run(() => HeartbeatLoopAsync(token), CancellationToken.None);
   }

   private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
   {
      try
      {
         await SafeSendHeartbeatAsync(cancellationToken);

         using var timer = new PeriodicTimer(Options.HeartbeatInterval, _timeProvider);
         while (await timer.WaitForNextTickAsync(cancellationToken))
            await SafeSendHeartbeatAsync(cancellationToken);
      }
      catch (OperationCanceledException)
      {
         // Heartbeat loop cancelled during shutdown; nothing to do.
      }
   }

   private async Task SafeSendHeartbeatAsync(CancellationToken cancellationToken)
   {
      try
      {
         await SendHeartbeatAsync(cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         // A failed heartbeat must never bubble into app code; best-effort log and retry next tick.
         _logger?.LogWarning(exception, "TranslationTools heartbeat failed.");
      }
   }

   internal async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
   {
      var payload = new HeartbeatRequest
      {
         ClientId = _clientId,
         Environment = NormalizedEnvironment(),
         Platform = PlatformName,
         Version = _clientVersion
      };

      var json = JsonSerializer.Serialize(payload, _serializerOptions);

      using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/translations/heartbeat")
      {
         Content = new StringContent(json, Encoding.UTF8, "application/json")
      };

      using var response = await _client.SendAsync(request, cancellationToken);
      response.EnsureSuccessStatusCode();
   }

   private string? NormalizedEnvironment()
   {
      return string.IsNullOrWhiteSpace(Options.Environment) ? null : Options.Environment!.Trim();
   }

   /// <summary>
   /// Push the declared global placeholder names for this deployment's Environment.
   /// Globals-only push: empty items (keys untouched), non-null globals (full-replaced for this Environment).
   /// </summary>
   internal async Task PushGlobalsAsync(string[] globals, CancellationToken cancellationToken = default)
   {
      var payload = new ProjectGlobalsPushRequest
      {
         Items = Array.Empty<object>(),
         Environment = NormalizedEnvironment(),
         Globals = globals
      };

      var json = JsonSerializer.Serialize(payload, _serializerOptions);

      using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/translations/project")
      {
         Content = new StringContent(json, Encoding.UTF8, "application/json")
      };

      using var response = await _client.SendAsync(request, cancellationToken);
      response.EnsureSuccessStatusCode();
   }

   private static string ResolveClientVersion()
   {
      var assembly = typeof(TranslationToolsClient).Assembly;

      var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
      if (!string.IsNullOrWhiteSpace(informational))
      {
         var plusIndex = informational.IndexOf('+');
         return plusIndex >= 0 ? informational[..plusIndex] : informational;
      }

      return assembly.GetName().Version?.ToString() ?? "unknown";
   }

   private sealed class HeartbeatRequest
   {
      public Guid ClientId { get; init; }
      public string? Environment { get; init; }
      public required string Platform { get; init; }
      public required string Version { get; init; }
   }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, CultureInfo.CurrentUICulture, cancellationToken);
   }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, locale, defaultValue: null, localeValues: null, cancellationToken);
   }

   /// <inheritdoc />
   public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      return GetInternalAsync(translation, locale, defaultValue, localeValues, cancellationToken);
   }

   /// <summary>
   /// Resolves the locale a lookup actually runs against. A locale whose name is blank (the
   /// invariant culture) is replaced by the configured <see cref="TranslationToolsClientOptions.DefaultLocale"/>.
   /// A locale the caller names is used as named.
   /// </summary>
   private string ResolveEffectiveLocale(CultureInfo locale)
   {
      return string.IsNullOrWhiteSpace(locale.Name) ? Options.DefaultLocale : locale.Name;
   }

   internal async Task<TranslationItemResponse> GetInternalAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      var localeName = ResolveEffectiveLocale(locale);

      var cached = await GetCachedTranslationAsync(localeName, translation, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchTranslationAsync(localeName, translation, defaultValue, localeValues, cancellationToken);
      var stored = await StoreTranslationAsync(localeName, translation, fetched, cancellationToken);
      return stored;
   }

   /// <inheritdoc />
   public async Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var localeName = ResolveEffectiveLocale(locale);
      var cached = await GetCachedLocaleAsync(localeName, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      return await StoreLocaleAsync(localeName, fetched, cancellationToken);
   }

   internal async Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var localeName = ResolveEffectiveLocale(locale);
      var fetched = await FetchLocaleAsync(localeName, cancellationToken);
      await StoreLocaleAsync(localeName, fetched, cancellationToken);
   }

   /// <summary>
   /// Try to get a cached translation for a specific locale.
   /// </summary>
   internal TranslationItemResponse? TryGetCached(TranslationRef translation, CultureInfo locale)
   {
      return _cache.Get(ResolveEffectiveLocale(locale), translation)?.Value;
   }

   internal void InvalidateLocale(CultureInfo locale)
   {
      InvalidateLocaleAsync(ResolveEffectiveLocale(locale), CancellationToken.None).GetAwaiter().GetResult();
   }

   internal void Invalidate(TranslationRef translation, CultureInfo locale)
   {
      InvalidateAsync(translation, ResolveEffectiveLocale(locale), CancellationToken.None).GetAwaiter().GetResult();
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
      if (_heartbeatCts is not null)
      {
         try
         {
            _heartbeatCts.Cancel();
         }
         catch (ObjectDisposedException)
         {
            // Already disposed; nothing to cancel.
         }

         _heartbeatCts.Dispose();
      }

      _client.Dispose();
      _initializeLock.Dispose();
   }

   private async Task<TranslationItemResponse[]> FetchLocaleAsync(string locale, CancellationToken cancellationToken)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(locale)}";

      var environment = NormalizedEnvironment();
      if (environment is not null)
         url += $"/{Uri.EscapeDataString(environment)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
      return await FetchAsync(request, static content => DeserializeAsync<TranslationItemResponse[]>(content), cancellationToken);
   }

   private async Task<TranslationItemResponse> FetchTranslationAsync(string locale, TranslationRef translation, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken)
   {
      var url = $"api/v1/translations/{Uri.EscapeDataString(translation.Origin)}/{Uri.EscapeDataString(locale)}/{Uri.EscapeDataString(translation.Key)}";

      var environment = NormalizedEnvironment();
      if (environment is not null)
         url += $"/{Uri.EscapeDataString(environment)}";

      var query = new List<string>();
      if (defaultValue is not null)
         query.Add($"defaultValue={Uri.EscapeDataString(defaultValue)}");

      if (localeValues is { Count: > 0 })
      {
         foreach (var pair in localeValues)
         {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrEmpty(pair.Value))
               continue;

            query.Add($"localeValues[{Uri.EscapeDataString(pair.Key)}]={Uri.EscapeDataString(pair.Value!)}");
         }
      }

      if (query.Count > 0)
         url += "?" + string.Join("&", query);

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
         new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>
         {
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
      var configured = Options.SupportedLocales.Length == 0
         ? new[] { CultureInfo.CurrentUICulture }
         : Options.SupportedLocales;

      // Skip the invariant culture (empty name): it has no locale to prefetch and would
      // fail locale normalization. This keeps Initialize() from throwing when the process
      // runs under the invariant culture (common on CI runners and globalization-invariant hosts).
      return configured.Where(static locale => !string.IsNullOrWhiteSpace(locale.Name)).ToArray();
   }

   private static async Task<T?> DeserializeAsync<T>(HttpContent content) where T : class
   {
      await using var stream = await content.ReadAsStreamAsync();
      return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions);
   }

}
