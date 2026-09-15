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
   private readonly TimeProvider _timeProvider;
   private readonly ILogger? _logger;
   private readonly Guid _clientId;
   private readonly string? _environment;
   private readonly SemaphoreSlim _initializeLock = new(1, 1);
   private readonly LookupSuppressionWindow _suppression;

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
      _suppression = new LookupSuppressionWindow(_timeProvider);

      if (string.IsNullOrWhiteSpace(Options.ApiKey))
         throw new ArgumentException("ApiKey is required.", nameof(options));

      EffectiveLocale.ValidateDefault(Options.DefaultLocale, nameof(options));
      _environment = TranslationClientInputValidator.NormalizeEnvironment(Options.Environment, nameof(options));

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

   /// <summary>
   /// Push the declared global placeholder names for this deployment's Environment.
   /// Globals-only push: empty items (keys untouched), non-null globals (full-replaced for this Environment).
   /// </summary>
   internal async Task PushGlobalsAsync(string[] globals, CancellationToken cancellationToken = default)
   {
      var payload = new ProjectGlobalsPushRequest
      {
         Items = Array.Empty<object>(),
         Environment = _environment,
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
   /// Resolves the locale a lookup actually runs against, against this client's configured
   /// <see cref="TranslationToolsClientOptions.DefaultLocale"/>. Every path that needs a locale name
   /// goes through here, because <see cref="EffectiveLocale"/> is the only thing the cache and the
   /// request builder accept.
   /// </summary>
   private EffectiveLocale ResolveEffectiveLocale(CultureInfo locale)
   {
      return EffectiveLocale.Resolve(locale, Options.DefaultLocale);
   }

   internal async Task<TranslationItemResponse> GetInternalAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      var effectiveLocale = ResolveEffectiveLocale(locale);

      var cached = await _cache.GetAsync(effectiveLocale, translation, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var lookup = new TranslationLookupRequest(translation, effectiveLocale, defaultValue, localeValues);

      var (fetched, degraded) = await FetchTranslationOrFallbackAsync(lookup, cancellationToken);
      if (degraded)
         return fetched;

      return await StoreTranslationAsync(effectiveLocale, fetched, cancellationToken);
   }

   /// <inheritdoc />
   public async Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var effectiveLocale = ResolveEffectiveLocale(locale);
      var cached = await _cache.GetLocaleAsync(effectiveLocale, cancellationToken);
      if (cached is not null)
         return cached.Value;

      var fetched = await FetchLocaleAsync(effectiveLocale, cancellationToken);
      return await StoreLocaleAsync(effectiveLocale, fetched, cancellationToken);
   }

   internal async Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var effectiveLocale = ResolveEffectiveLocale(locale);
      var fetched = await FetchLocaleAsync(effectiveLocale, cancellationToken);
      await StoreLocaleAsync(effectiveLocale, fetched, cancellationToken);
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
            // Already disposed; nothing to cancel.
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
}
