using AwesomeAssertions;
using Fixture.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client.Internal;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

[Collection("PlaceholderRuntime")]
public class KeylessModeTests
{
   private const string ORIGIN = "SourceGeneratorEndToEnd:/Localizations.resx";
   private const string CLIENT_CATEGORY = "mvdmio.TranslationTools.Client.ITranslationToolsClient";
   private const string KEYLESS_WARNING = "TranslationTools client has no API key and AllowMissingApiKey is set. Every lookup answers with the local fallback, and nothing is sent to the service.";
   private const string KEYLESS_ERROR = "TranslationTools client has no API key (AllowMissingApiKey is set), so it cannot call the service.";

   [Fact]
   public async Task KeylessLookup_AfterStartup_AnswersTheNeutralValueUnderEn_AndSendsNothing()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");

      WithUICulture("en", () => Localizations.Button_Save.Should().Be("Save"));

      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessLookup_UnderNl_AnswersTheDutchValue()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");

      WithUICulture("nl", () => Localizations.Button_Save.Should().Be("Opslaan"));

      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Theory]
   [InlineData("")]
   [InlineData("   ")]
   public async Task KeylessLookup_WithBlankKey_AnswersTheFallback(string apiKey)
   {
      using var fixture = await StartAsync(options => options.ApiKey = apiKey);

      WithUICulture("nl", () => Localizations.Button_Save.Should().Be("Opslaan"));
   }

   [Fact]
   public async Task KeylessLookup_ThroughTheClient_AnswersTheLocalFallbackForTheAskedLocale()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<ITranslationToolsClient>();
      var localeValues = new Dictionary<string, string?> { ["nl"] = "Opslaan" };

      var dutch = await client.GetAsync(new TranslationRef(ORIGIN, "Button.Save"), new CultureInfo("nl"), "Save", localeValues, TestContext.Current.CancellationToken);
      var english = await client.GetAsync(new TranslationRef(ORIGIN, "Button.Save"), new CultureInfo("en"), "Save", localeValues, TestContext.Current.CancellationToken);

      dutch.Value.Should().Be("Opslaan");
      english.Value.Should().Be("Save");
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessLookup_OfAKeyWithNoLocalFallback_AnswersNullFromTheClient_AndTheKeyNameFromTranslationsGet()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<ITranslationToolsClient>();
      var missing = new TranslationRef(ORIGIN, "Missing.Key");

      var response = await client.GetAsync(missing, new CultureInfo("en"), TestContext.Current.CancellationToken);

      response.Value.Should().BeNull();
      Translations.Get(missing, new CultureInfo("en")).Should().Be("Missing.Key");
   }

   [Fact]
   public async Task KeylessLookup_WithThrowOnLookupError_StillAnswersTheFallback()
   {
      using var fixture = await StartAsync(options =>
      {
         options.ApiKey = "";
         options.ThrowOnLookupError = true;
      });

      WithUICulture("nl", () => Localizations.Button_Save.Should().Be("Opslaan"));
   }

   [Fact]
   public async Task KeylessStartup_WithLiveUpdatesAndHeartbeat_SendsNoRequest_EvenAfterOneHeartbeatInterval()
   {
      using var fixture = await StartAsync(options =>
      {
         options.ApiKey = "";
         options.EnableLiveUpdates = true;
         options.EnableHeartbeat = true;
         options.HeartbeatInterval = TimeSpan.FromHours(1);
      });

      fixture.Time.Advance(TimeSpan.FromHours(2));
      await Task.Delay(100, TestContext.Current.CancellationToken);

      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessStartup_LogsOneWarningUnderTheClientCategory_AndNothingPerLookup()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");

      WithUICulture("nl", () => Localizations.Button_Save.Should().Be("Opslaan"));
      WithUICulture("en", () => Localizations.Button_Save.Should().Be("Save"));

      var warning = fixture.Logs.Entries.Where(entry => entry.Category.StartsWith("mvdmio.TranslationTools", StringComparison.Ordinal))
         .Should().ContainSingle().Subject;
      warning.Category.Should().Be(CLIENT_CATEGORY);
      warning.Level.Should().Be(LogLevel.Warning);
      warning.Message.Should().Be(KEYLESS_WARNING);
   }

   [Fact]
   public async Task KeylessStartup_RunTwice_LogsTheWarningEachTime()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");

      await fixture.Host.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      fixture.Logs.Entries.Where(entry => entry.Message == KEYLESS_WARNING).Should().HaveCount(2);
   }

   [Fact]
   public async Task KeylessClient_WriteAsync_ThrowsBeforeAnyRequest_EvenForNoValues()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<ITranslationToolsClient>();

      var act = () => client.WriteAsync([], TestContext.Current.CancellationToken);

      (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(KEYLESS_ERROR);
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessClient_DeclareAsync_ThrowsBeforeAnyRequest_EvenForNoKeys()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<ITranslationToolsClient>();

      var act = () => client.DeclareAsync([], TestContext.Current.CancellationToken);

      (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(KEYLESS_ERROR);
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessClient_GetStoredValueAsync_ThrowsBeforeAnyRequest()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<ITranslationToolsClient>();

      var act = () => client.GetStoredValueAsync(new TranslationRef(ORIGIN, "Button.Save"), new CultureInfo("en"), TestContext.Current.CancellationToken);

      (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(KEYLESS_ERROR);
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task KeylessClient_GetLocaleAsync_ThrowsBeforeAnyRequest_EvenWhenTheCacheHoldsTheLocale()
   {
      using var fixture = await StartAsync(options => options.ApiKey = "");
      var client = fixture.Host.Services.GetRequiredService<TranslationToolsClient>();
      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?> { [new TranslationRef(ORIGIN, "Button.Save")] = "Cached" },
         TestContext.Current.CancellationToken
      );

      var act = () => client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(KEYLESS_ERROR);
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task BlankKeyWithTheOptionOff_LogsApiKeyIsRequired_AndNoClientIsBuilt()
   {
      using var fixture = await StartAsync(options =>
      {
         options.ApiKey = "";
         options.AllowMissingApiKey = false;
      });

      var error = fixture.Logs.Entries.Where(entry => entry.Category == CLIENT_CATEGORY).Should().ContainSingle().Subject;
      error.Level.Should().Be(LogLevel.Error);
      error.Message.Should().Be("Error initializing translation tools client");
      error.Exception.Should().BeOfType<ArgumentException>().Which.Message.Should().StartWith("ApiKey is required.");
      fixture.Handler.Requests.Should().BeEmpty();
   }

   [Fact]
   public async Task RealKeyWithTheOptionOn_Initialize_SendsTheDeclarationPreloadsAndHeartbeatsWithTheKey()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN, "Button.Save", neutralValue: "Save") };
      using var client = new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            AllowMissingApiKey = true,
            EnableHeartbeat = true,
            HeartbeatInterval = TimeSpan.FromHours(1),
            SupportedLocales = [new CultureInfo("en")]
         }),
         new LocalTranslationToolsClientCache(),
         new FakeTimeProvider(),
         new FileClientIdStore(Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"))),
         catalogSource: () => catalog
      );

      await client.Initialize(TestContext.Current.CancellationToken);
      await handler.HeartbeatSeen.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

      handler.Requests.Should().Equal("POST /api/v1/translations/declaration", "GET /api/v1/translations/en", "POST /api/v1/translations/heartbeat");
      handler.Authorizations.Should().OnlyContain(value => value == "api-key");
   }

   private static async Task<KeylessFixture> StartAsync(Action<TranslationToolsClientOptions> configure)
   {
      var handler = new RecordingHandler();
      var logs = new CategoryLoggerProvider();
      var time = new FakeTimeProvider();

      var builder = Host.CreateApplicationBuilder();
      builder.Logging.ClearProviders();
      builder.Logging.AddProvider(logs);
      builder.Services.AddSingleton<IClientIdStore>(new FileClientIdStore(Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"))));
      builder.Services.AddSingleton<TimeProvider>(time);
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.SupportedLocales = [new CultureInfo("en")];
         options.AllowMissingApiKey = true;
         configure(options);
      });
      builder.Services.AddHttpClient(nameof(TranslationToolsClient)).ConfigurePrimaryHttpMessageHandler(() => handler);

      var host = builder.Build();
      await host.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      return new KeylessFixture(host, handler, logs, time);
   }

   private static void WithUICulture(string locale, Action body)
   {
      var previous = CultureInfo.CurrentUICulture;

      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo(locale);
         body();
      }
      finally
      {
         CultureInfo.CurrentUICulture = previous;
      }
   }

   private sealed class KeylessFixture(IHost host, RecordingHandler handler, CategoryLoggerProvider logs, FakeTimeProvider time) : IDisposable
   {
      public IHost Host { get; } = host;

      public RecordingHandler Handler { get; } = handler;

      public CategoryLoggerProvider Logs { get; } = logs;

      public FakeTimeProvider Time { get; } = time;

      public void Dispose()
      {
         Host.Dispose();
      }
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      private readonly ConcurrentQueue<string> _requests = new();
      private readonly ConcurrentQueue<string?> _authorizations = new();
      private readonly TaskCompletionSource _heartbeatSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

      public IReadOnlyCollection<string> Requests => _requests;

      public IReadOnlyCollection<string?> Authorizations => _authorizations;

      public Task HeartbeatSeen => _heartbeatSeen.Task;

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         var path = request.RequestUri?.AbsolutePath ?? string.Empty;

         _requests.Enqueue($"{request.Method} {path}");
         _authorizations.Enqueue(request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null);

         if (path.EndsWith("/heartbeat", StringComparison.Ordinal))
            _heartbeatSeen.TrySetResult();

         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
         });
      }
   }

   private sealed class CategoryLoggerProvider : ILoggerProvider
   {
      public ConcurrentQueue<CategorizedLogEntry> Entries { get; } = new();

      public ILogger CreateLogger(string categoryName)
      {
         return new CategoryLogger(categoryName, Entries);
      }

      public void Dispose()
      {
      }

      private sealed class CategoryLogger(string category, ConcurrentQueue<CategorizedLogEntry> entries) : ILogger
      {
         public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

         public bool IsEnabled(LogLevel logLevel) => true;

         public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
         {
            entries.Enqueue(new CategorizedLogEntry(category, logLevel, formatter(state, exception), exception));
         }
      }
   }

   private sealed record CategorizedLogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
}
