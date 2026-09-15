using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
      TranslationClientInputValidator.NormalizeEnvironment(Options.Environment, nameof(options));

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

   private async Task<TranslationItemResponse[]> FetchLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken)
   {
      if (_suppression.IsOpen)
         throw new TranslationLookupException($"Translation lookup for locale '{locale.Name}' failed: {TranslationLookupFailure.Suppressed.Reason}.");

      var url = $"api/v1/translations/{Uri.EscapeDataString(locale.Name)}";

      var environment = NormalizedEnvironment();
      if (environment is not null)
         url += $"/{Uri.EscapeDataString(environment)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
      return await FetchAsync<TranslationItemResponse[]>(request, cancellationToken);
   }

   /// <summary>
   /// Fetches a single translation. Never throws (other than for the caller's own cancellation, and
   /// unless <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> is set): an unsuccessful
   /// response, a transport exception, a timeout, and an undeserialisable body are all logged and
   /// answered with the local fallback instead. Degraded results are never cached, so the next call
   /// retries the service.
   ///
   /// Bounded by <see cref="TranslationToolsClientOptions.LookupTimeout"/>, applied as a
   /// <see cref="TimeProvider"/>-derived cancellation token linked to the caller's own token —
   /// never by setting <see cref="HttpClient.Timeout"/>, which belongs to the consumer that
   /// supplied the <see cref="HttpClient"/>. The bound covers reading the response body as well as
   /// getting its headers. A caller's own cancellation is distinguished from the client's timeout
   /// and always propagates rather than degrading.
   /// </summary>
   private async Task<(TranslationItemResponse Value, bool Degraded)> FetchTranslationOrFallbackAsync(TranslationLookupRequest lookup, CancellationToken cancellationToken)
   {
      if (_suppression.IsOpen)
         return HandleLookupFailure(lookup, TranslationLookupFailure.Suppressed, exception: null);

      using var request = lookup.ToHttpRequest(NormalizedEnvironment());

      using var timeoutCts = new CancellationTokenSource(Options.LookupTimeout, _timeProvider);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

      try
      {
         return (await FetchAsync<TranslationItemResponse>(request, linkedCts.Token), false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         // The caller asked to stop. That is not a failure, and it is never swallowed.
         throw;
      }
      catch (Exception exception)
      {
         var failure = TranslationLookupFailure.Classify(exception);

         if (failure.OpensSuppressionWindow)
            _suppression.Open();

         return HandleLookupFailure(lookup, failure, exception);
      }
   }

   /// <summary>
   /// Logs a classified failure at its level and answers the lookup with the local fallback.
   ///
   /// With <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> set it rethrows instead,
   /// preserving the exception the fetch produced so an application that opts back into throwing
   /// catches the same types it caught before this contract existed: an
   /// <see cref="HttpRequestException"/> for an unsuccessful response or a connection failure, a
   /// <see cref="JsonException"/> for a body the client cannot read. The client's own timeout and
   /// the suppression window have no such exception to preserve, and surface as a
   /// <see cref="TranslationLookupException"/>, which also keeps a timeout distinguishable from the
   /// caller's own cancellation.
   /// </summary>
   private (TranslationItemResponse Value, bool Degraded) HandleLookupFailure(TranslationLookupRequest lookup, TranslationLookupFailure failure, Exception? exception)
   {
      if (Options.ThrowOnLookupError)
      {
         if (exception is not null and not OperationCanceledException)
            ExceptionDispatchInfo.Capture(exception).Throw();

         throw new TranslationLookupException(
            $"Translation lookup for key '{lookup.Translation.Key}' in locale '{lookup.Locale.Name}' failed: {failure.Reason}.",
            exception
         );
      }

      _logger?.Log(
         failure.Level,
         exception,
         "Translation lookup for key '{Key}' in locale '{Locale}' degraded to local fallback: {Reason}.",
         lookup.Translation.Key,
         lookup.Locale.Name,
         failure.Reason
      );

      return (lookup.LocalFallback(), true);
   }

   private async Task<T> FetchAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken) where T : class
   {
      using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      return await DeserializeAsync<T>(response.Content, cancellationToken) ?? throw new InvalidOperationException("Response body was empty.");
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

   private static async Task<T?> DeserializeAsync<T>(HttpContent content, CancellationToken cancellationToken) where T : class
   {
      await using var stream = await content.ReadAsStreamAsync(cancellationToken);
      return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
   }
}
