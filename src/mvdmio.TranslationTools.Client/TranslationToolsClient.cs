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
public sealed partial class TranslationToolsClient : ITranslationToolsClient, IDisposable
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
   private readonly Func<IReadOnlyList<TranslationCatalogKey>> _catalogSource;
   private readonly TimeProvider _timeProvider;
   private readonly ILogger? _logger;
   private readonly Guid _clientId;
   private readonly string? _environment;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);
   private readonly LookupSuppressionWindow _suppression;
   private readonly bool _isKeyless;

   private CancellationTokenSource? _heartbeatCts;
   private int _heartbeatStarted;

   private TranslationToolsClientOptions Options => _options.Value;

   private Uri BaseUri => new(Options.BaseUrlOverride);

   /// <summary>
   /// Create a client using cache services registered in the container.
   /// Uses the assembly-level <see cref="TranslationCatalog"/> registered by generated code.
   /// </summary>
   public TranslationToolsClient(HttpClient client, IOptions<TranslationToolsClientOptions> options, ILogger<TranslationToolsClient>? logger = null)
      : this(client, options, new LocalTranslationToolsClientCache(), catalogSource: TranslationCatalog.DiscoverEntries, logger: logger)
   {
   }

   internal TranslationToolsClient(
      HttpClient client,
      IOptions<TranslationToolsClientOptions> options,
      ITranslationToolsClientCache cache,
      TimeProvider? timeProvider = null,
      IClientIdStore? clientIdStore = null,
      ILogger? logger = null,
      Func<IReadOnlyList<TranslationCatalogKey>>? catalogSource = null)
   {
      _client = client;
      _options = options;
      _cache = cache;
      _catalogSource = catalogSource ?? (() => Array.Empty<TranslationCatalogKey>());
      _timeProvider = timeProvider ?? TimeProvider.System;
      _logger = logger;
      _clientId = (clientIdStore ?? new FileClientIdStore()).GetOrCreateClientId();
      _suppression = new LookupSuppressionWindow(_timeProvider);

      _isKeyless = Options.IsKeyless;

      if (!_isKeyless && string.IsNullOrWhiteSpace(Options.ApiKey))
         throw new ArgumentException("ApiKey is required.", nameof(options));

      EffectiveLocale.ValidateDefault(Options.DefaultLocale, nameof(options));
      _environment = TranslationClientInputValidator.NormalizeEnvironment(Options.Environment, nameof(options));

      _client.BaseAddress = BaseUri;

      if (!_isKeyless)
         _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Options.ApiKey);
   }

   internal bool IsKeyless => _isKeyless;

   private void ThrowIfKeyless()
   {
      if (_isKeyless)
         throw new InvalidOperationException("TranslationTools client has no API key (AllowMissingApiKey is set), so it cannot call the service.");
   }

   /// <inheritdoc />
   public async Task Initialize(CancellationToken cancellationToken = default)
   {
      if (_isKeyless)
         return;

      await _initializeLock.WaitAsync(cancellationToken);

      try
      {
         await TrySendDeclarationAsync(cancellationToken);

         foreach (var locale in GetLocalesToPreload())
            await TryPreloadLocaleAsync(locale, cancellationToken);
      }
      finally
      {
         _initializeLock.Release();
      }

      StartHeartbeat();
   }

   internal async Task PushGlobalsAsync(string[] globals, CancellationToken cancellationToken = default)
   {
      var payload = new ProjectPushRequest
      {
         Items = Array.Empty<KeyValueItemRequest>(),
         Environment = _environment,
         Globals = globals
      };

      await PostJsonAsync("api/v1/translations/project", payload, cancellationToken);
   }

   private async Task PostJsonAsync<TPayload>(string path, TPayload payload, CancellationToken cancellationToken)
   {
      var json = JsonSerializer.Serialize(payload, _serializerOptions);

      using var request = new HttpRequestMessage(HttpMethod.Post, path)
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

   private EffectiveLocale ResolveEffectiveLocale(CultureInfo locale)
   {
      return EffectiveLocale.Resolve(locale, Options.DefaultLocale);
   }

   internal async Task<TranslationItemResponse> GetInternalAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      var effectiveLocale = ResolveEffectiveLocale(locale);
      var lookup = new TranslationLookupRequest(translation, effectiveLocale, defaultValue, localeValues);

      if (_isKeyless)
         return lookup.LocalFallback();

      var cached = await _cache.GetAsync(effectiveLocale, translation, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var (fetched, degraded) = await FetchTranslationOrFallbackAsync(lookup, cancellationToken);
      if (degraded)
         return fetched;

      return await StoreTranslationAsync(effectiveLocale, fetched, cancellationToken);
   }

   /// <inheritdoc />
   public async Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      ThrowIfKeyless();

      var effectiveLocale = ResolveEffectiveLocale(locale);
      var cached = await _cache.GetLocaleAsync(effectiveLocale, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchLocaleAsync(effectiveLocale, cancellationToken);
      return await StoreLocaleAsync(effectiveLocale, fetched, cancellationToken);
   }

   private async Task TryPreloadLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken)
   {
      try
      {
         await RefreshLocaleAsync(locale, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         _logger?.LogWarning(exception, "TranslationTools preload of locale {Locale} failed.", locale.Name);
      }
   }

   internal Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return RefreshLocaleAsync(ResolveEffectiveLocale(locale), cancellationToken);
   }

   private async Task RefreshLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken)
   {
      var fetched = await FetchLocaleAsync(locale, cancellationToken);
      await StoreLocaleAsync(locale, fetched, cancellationToken);
   }

   internal TranslationItemResponse? TryGetCached(TranslationRef translation, CultureInfo locale)
   {
      return _cache.Get(ResolveEffectiveLocale(locale), translation)?.Value;
   }

   internal void InvalidateLocale(CultureInfo locale)
   {
      _cache.RemoveLocaleAsync(ResolveEffectiveLocale(locale), CancellationToken.None).GetAwaiter().GetResult();
   }

   internal void Invalidate(TranslationRef translation, CultureInfo locale)
   {
      _cache.RemoveAsync(ResolveEffectiveLocale(locale), translation, CancellationToken.None).GetAwaiter().GetResult();
   }

   internal Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<TranslationRef, string?> values, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(values);

      return StoreLocaleAsync(
         ResolveEffectiveLocale(locale),
         values.Select(static item => new TranslationItemResponse
         {
            Origin = item.Key.Origin,
            Key = item.Key.Key,
            Value = item.Value
         }).ToArray(),
         cancellationToken
      );
   }

   internal Task ApplyUpdateAsync(TranslationRef translation, string? value, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return StoreTranslationAsync(
         ResolveEffectiveLocale(locale),
         new TranslationItemResponse
         {
            Origin = translation.Origin,
            Key = translation.Key,
            Value = value
         },
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
         }

         _heartbeatCts.Dispose();
      }

      _client.Dispose();
      _initializeLock.Dispose();
   }

   private async Task<TranslationLocaleSnapshot> StoreLocaleAsync(EffectiveLocale locale, TranslationItemResponse[] fetched, CancellationToken cancellationToken)
   {
      var stored = new TranslationLocaleSnapshot(
         locale.Name,
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

   private async Task<TranslationItemResponse> StoreTranslationAsync(EffectiveLocale locale, TranslationItemResponse item, CancellationToken cancellationToken)
   {
      await _cache.SetAsync(locale, new TranslationToolsClientCacheEntry<TranslationItemResponse> { Value = item }, cancellationToken);

      return item;
   }

   private EffectiveLocale[] GetLocalesToPreload()
   {
      var configured = Options.SupportedLocales.Length == 0
         ? new[] { CultureInfo.CurrentUICulture }
         : Options.SupportedLocales;

      return configured
         .Select(ResolveEffectiveLocale)
         .DistinctBy(static locale => locale.Name, StringComparer.Ordinal)
         .ToArray();
   }
}
