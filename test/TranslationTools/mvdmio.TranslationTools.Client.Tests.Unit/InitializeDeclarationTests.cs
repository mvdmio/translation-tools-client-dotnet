using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client.Internal;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class InitializeDeclarationTests
{
   private const string ORIGIN_A = "Fixture.App:/Localizations.resx";
   private const string ORIGIN_B = "Fixture.App:/Resources/Shared/Errors.resx";

   private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

   [Fact]
   public async Task Initialize_SendsOneDeclarationWithNeutralAndSiblingValues()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            ORIGIN_A,
            "Button.Save",
            neutralValue: "Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nl"] = "Opslaan" }
         )
      };

      using var client = CreateClient(handler, catalog, environment: "staging", supportedLocales: [new CultureInfo("en"), new CultureInfo("nl")]);

      await client.Initialize(TestContext.Current.CancellationToken);

      var declaration = handler.Declarations.Should().ContainSingle().Subject;
      declaration.Environment.Should().Be("staging");
      declaration.Items.Should().BeEquivalentTo(
         [
            new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Save"),
            new DeclarationItem(ORIGIN_A, "nl", "Button.Save", "Opslaan")
         ],
         options => options.WithStrictOrdering()
      );
   }

   [Fact]
   public async Task Initialize_SendsDeclarationBeforeTheFirstLocaleRequest()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en"), new CultureInfo("nl")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Requests.Should().Equal(
         "POST /api/v1/translations/declaration",
         "GET /api/v1/translations/en",
         "GET /api/v1/translations/nl"
      );
   }

   [Fact]
   public async Task Initialize_DeclaresKeysTheServiceAlreadyReturns()
   {
      var handler = new RecordingHandler
      {
         LocaleResponses =
         {
            ["en"] =
            [
               new TranslationItemResponse { Origin = ORIGIN_A, Key = "Button.Save", Value = "Service Save" },
               new TranslationItemResponse { Origin = ORIGIN_A, Key = "Only.Remote", Value = "remote" }
            ]
         }
      };

      var catalog = new[]
      {
         new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save"),
         new TranslationCatalogKey(ORIGIN_A, "Button.Cancel", neutralValue: "Cancel")
      };

      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().ContainSingle().Subject.Items.Should().BeEquivalentTo(
         [
            new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Save"),
            new DeclarationItem(ORIGIN_A, "en", "Button.Cancel", "Cancel")
         ]
      );
   }

   [Fact]
   public async Task Initialize_KeyWithOnlyEmptyValues_IsDeclaredOnceWithAnEmptyValue()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            ORIGIN_A,
            "Button.Empty",
            neutralValue: "",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nl"] = "" }
         )
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().ContainSingle().Subject.Items.Should().ContainSingle()
         .Which.Should().BeEquivalentTo(new DeclarationItem(ORIGIN_A, "en", "Button.Empty", ""));
   }

   [Fact]
   public async Task Initialize_TwoOriginsSharingKeyName_BothAreDeclared()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save A"),
         new TranslationCatalogKey(ORIGIN_B, "Button.Save", neutralValue: "Save B")
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().ContainSingle().Subject.Items.Should().BeEquivalentTo(
         [
            new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Save A"),
            new DeclarationItem(ORIGIN_B, "en", "Button.Save", "Save B")
         ]
      );
   }

   [Fact]
   public async Task Initialize_SiblingLocaleEqualToDefault_WinsForThatLocale()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            ORIGIN_A,
            "Button.Save",
            neutralValue: "Neutral Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Sibling Save" }
         )
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().ContainSingle().Subject.Items.Should().ContainSingle()
         .Which.Should().BeEquivalentTo(new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Sibling Save"));
   }

   [Fact]
   public async Task Initialize_SiblingLocaleThatIsNotARuntimeCulture_IsDeclared()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            ORIGIN_A,
            "Button.Save",
            neutralValue: "Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["zz-x-notreal"] = "X" }
         )
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().ContainSingle().Subject.Items.Should().BeEquivalentTo(
         [
            new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Save"),
            new DeclarationItem(ORIGIN_A, "zz-x-notreal", "Button.Save", "X")
         ]
      );
   }

   [Fact]
   public async Task Initialize_OmitsEnvironment_WhenUnnamed()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, environment: null, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.DeclarationRawBodies.Should().ContainSingle()
         .Which.Should().NotContain("environment");
   }

   [Fact]
   public async Task Initialize_DeclarationBody_HasNeitherPruneNorGlobals()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, environment: "staging", supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      var raw = handler.DeclarationRawBodies.Should().ContainSingle().Subject;
      raw.Should().NotContain("prune").And.NotContain("globals");
   }

   [Fact]
   public async Task Initialize_EmptyCatalog_SendsNoDeclaration()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, catalog: [], supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().BeEmpty();
   }

   [Fact]
   public async Task Initialize_WithoutSupportedLocale_StillSendsDeclaration()
   {
      var originalCulture = CultureInfo.CurrentUICulture;
      try
      {
         CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

         var handler = new RecordingHandler();
         var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
         using var client = CreateClient(handler, catalog, supportedLocales: [], enableHeartbeat: false);

         await client.Initialize(TestContext.Current.CancellationToken);

         handler.Declarations.Should().ContainSingle();
      }
      finally
      {
         CultureInfo.CurrentUICulture = originalCulture;
      }
   }

   [Theory]
   [InlineData(HttpStatusCode.InternalServerError)]
   [InlineData(HttpStatusCode.NotFound)]
   public async Task Initialize_FailedDeclaration_DoesNotThrow_IsLoggedOnce_PreloadRuns_AndHeartbeatStarts(HttpStatusCode statusCode)
   {
      var handler = new RecordingHandler { DeclarationStatusCode = statusCode };
      var logger = new FakeLogger();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], logger: logger);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);
      await act.Should().NotThrowAsync();

      await WaitForHeartbeatsAsync(handler, 1);
      logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning)
         .Which.Message.Should().Contain("declaration", Exactly.Once());
      handler.Requests.Should().Contain("GET /api/v1/translations/en");
   }

   [Fact]
   public async Task Initialize_DeclarationThrows_DoesNotThrow_IsLoggedOnce_PreloadRuns_AndHeartbeatStarts()
   {
      var handler = new RecordingHandler { DeclarationException = new HttpRequestException("connection refused") };
      var logger = new FakeLogger();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], logger: logger);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);
      await act.Should().NotThrowAsync();

      await WaitForHeartbeatsAsync(handler, 1);
      logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning)
         .Which.Message.Should().Contain("declaration", Exactly.Once());
      handler.Requests.Should().Contain("GET /api/v1/translations/en");
   }

   [Fact]
   public async Task Initialize_FailedLocaleGet_DoesNotThrow_AndLeavesTheDeclarationSent()
   {
      var handler = new RecordingHandler { LocaleStatusCode = HttpStatusCode.InternalServerError };
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);

      await act.Should().NotThrowAsync();
      handler.Declarations.Should().ContainSingle();
   }

   [Fact]
   public async Task Initialize_LocaleRequestTimesOut_IsAFailedRequest_AndStartsTheHeartbeat()
   {
      var handler = new RecordingHandler { OnLocaleRequest = static () => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.") };
      var logger = new FakeLogger();
      using var client = CreateClient(handler, supportedLocales: [new CultureInfo("en")], logger: logger);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);

      await act.Should().NotThrowAsync();
      await WaitForHeartbeatsAsync(handler, 1);
      logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning)
         .Which.Message.Should().Contain("en");
   }

   [Fact]
   public async Task Initialize_CancelledWhilePreloading_ThrowsOperationCanceled_AndStartsNoHeartbeat()
   {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
      var handler = new RecordingHandler
      {
         OnLocaleRequest = () =>
         {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
         }
      };
      var logger = new FakeLogger();
      using var client = CreateClient(handler, supportedLocales: [new CultureInfo("en"), new CultureInfo("nl")], logger: logger);

      var act = async () => await client.Initialize(cts.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
      await Task.Delay(100, TestContext.Current.CancellationToken);
      handler.Heartbeats.Should().Be(0);
      handler.Requests.Should().NotContain("GET /api/v1/translations/nl");
      logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
   }

   [Fact]
   public async Task Initialize_CancelledBeforeStart_ThrowsOperationCanceled_AndStartsNoHeartbeat()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, supportedLocales: [new CultureInfo("en")]);

      var act = async () => await client.Initialize(new CancellationToken(canceled: true));

      await act.Should().ThrowAsync<OperationCanceledException>();
      await Task.Delay(100, TestContext.Current.CancellationToken);
      handler.Heartbeats.Should().Be(0);
   }

   [Fact]
   public async Task Initialize_SecondCall_SendsTheDeclarationAgain()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);
      await client.Initialize(TestContext.Current.CancellationToken);

      handler.Declarations.Should().HaveCount(2);
   }

   [Fact]
   public async Task Initialize_AfterwardsLookupReturnsTheServiceValue_NotTheResxValue()
   {
      var handler = new RecordingHandler
      {
         LocaleResponses =
         {
            ["en"] = [new TranslationItemResponse { Origin = ORIGIN_A, Key = "Button.Save", Value = "Edited on service" }]
         }
      };
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      var result = await client.GetAsync(new TranslationRef(ORIGIN_A, "Button.Save"), new CultureInfo("en"), TestContext.Current.CancellationToken);

      result.Value.Should().Be("Edited on service");
      handler.SingleKeyGets.Should().Be(0);
   }

   [Fact]
   public async Task Initialize_KeyThePreloadDidNotReturn_FallsBackToTheSingleKeyLookup()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);
      await client.GetAsync(new TranslationRef(ORIGIN_A, "Button.Save"), new CultureInfo("en"), TestContext.Current.CancellationToken);

      handler.SingleKeyGets.Should().Be(1);
   }

   [Fact]
   public async Task Initialize_WithoutCatalogReplacement_DoesNotDeclareKeysFromGlobalCatalog()
   {
      TranslationCatalog.Replace(
      [
         new TranslationCatalogKey(ORIGIN_A, "Leaked.Key", neutralValue: "Leak")
      ]);

      try
      {
         var handler = new RecordingHandler();
         using var client = CreateClient(handler, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

         await client.Initialize(TestContext.Current.CancellationToken);

         handler.Declarations.Should().BeEmpty();
      }
      finally
      {
         TranslationCatalog.Clear();
      }
   }

   [Fact]
   public async Task Initialize_WithExplicitCatalog_DeclaresExactlyThatCatalog()
   {
      TranslationCatalog.Replace(
      [
         new TranslationCatalogKey(ORIGIN_A, "Leaked.Key", neutralValue: "Leak")
      ]);

      try
      {
         var handler = new RecordingHandler();
         var catalog = new[] { new TranslationCatalogKey(ORIGIN_A, "Button.Save", neutralValue: "Save") };
         using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

         await client.Initialize(TestContext.Current.CancellationToken);

         var declaration = handler.Declarations.Should().ContainSingle().Subject;
         declaration.Items.Should().BeEquivalentTo([new DeclarationItem(ORIGIN_A, "en", "Button.Save", "Save")]);
      }
      finally
      {
         TranslationCatalog.Clear();
      }
   }

   [Fact]
   public async Task Initialize_WithCatalogSource_ReadsTheCatalogWhenInitializeRuns()
   {
      var handler = new RecordingHandler();
      var keys = new List<TranslationCatalogKey>();
      using var client = CreateClient(handler, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false, catalogSource: () => keys.ToArray());

      keys.Add(new TranslationCatalogKey(ORIGIN_A, "Added.Late", neutralValue: "Late"));

      await client.Initialize(TestContext.Current.CancellationToken);

      var declaration = handler.Declarations.Should().ContainSingle().Subject;
      declaration.Items.Should().BeEquivalentTo([new DeclarationItem(ORIGIN_A, "en", "Added.Late", "Late")]);
   }

   [Fact]
   public async Task Initialize_WhenCatalogSourceThrows_LogsOneWarningAndStillPreloads()
   {
      var handler = new RecordingHandler();
      var logger = new FakeLogger();
      using var client = CreateClient(
         handler,
         supportedLocales: [new CultureInfo("en")],
         enableHeartbeat: false,
         logger: logger,
         catalogSource: () => throw new InvalidOperationException("boom")
      );

      var act = () => client.Initialize(TestContext.Current.CancellationToken);

      await act.Should().NotThrowAsync();
      handler.Declarations.Should().BeEmpty();
      handler.Requests.Should().Contain("GET /api/v1/translations/en");
      var warning = logger.Entries.Should().ContainSingle().Subject;
      warning.Level.Should().Be(LogLevel.Warning);
      warning.Message.Should().Contain("declaration");
   }

   private static TranslationToolsClient CreateClient(
      RecordingHandler handler,
      IEnumerable<TranslationCatalogKey>? catalog = null,
      string? environment = null,
      CultureInfo[]? supportedLocales = null,
      FakeTimeProvider? time = null,
      bool enableHeartbeat = true,
      ILogger? logger = null,
      ITranslationToolsClientCache? cache = null,
      Func<IReadOnlyList<TranslationCatalogKey>>? catalogSource = null)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(
            new TranslationToolsClientOptions
            {
               ApiKey = "api-key",
               DefaultLocale = "en",
               Environment = environment,
               EnableHeartbeat = enableHeartbeat,
               HeartbeatInterval = TimeSpan.FromHours(1),
               SupportedLocales = supportedLocales ?? []
            }
         ),
         cache ?? new LocalTranslationToolsClientCache(),
         time ?? new FakeTimeProvider(),
         new FileClientIdStore(Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"))),
         logger,
         catalogSource ?? (() => catalog?.ToArray() ?? [])
      );
   }

   private static async Task WaitForHeartbeatsAsync(RecordingHandler handler, int expected)
   {
      var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
      while (DateTime.UtcNow < deadline)
      {
         if (handler.Heartbeats >= expected)
            return;

         await Task.Delay(25);
      }

      throw new Xunit.Sdk.XunitException($"Timed out waiting for {expected} heartbeats. Saw {handler.Heartbeats}.");
   }

   private sealed record DeclarationItem(string Origin, string Locale, string Key, string? Value);

   private sealed class DeclarationBody
   {
      public DeclarationItem[] Items { get; init; } = [];
      public string? Environment { get; init; }
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      private readonly ConcurrentQueue<string> _requests = new();

      public HttpStatusCode LocaleStatusCode { get; set; } = HttpStatusCode.OK;
      public HttpStatusCode DeclarationStatusCode { get; set; } = HttpStatusCode.OK;
      public Exception? DeclarationException { get; set; }
      public Action? OnLocaleRequest { get; set; }

      public Dictionary<string, TranslationItemResponse[]> LocaleResponses { get; } = new(StringComparer.OrdinalIgnoreCase);

      public ConcurrentQueue<DeclarationBody> Declarations { get; } = new();
      public ConcurrentQueue<string> DeclarationRawBodies { get; } = new();

      public IReadOnlyCollection<string> Requests => _requests;

      public int Heartbeats { get; private set; }
      public int SingleKeyGets { get; private set; }

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         var path = request.RequestUri?.AbsolutePath ?? string.Empty;

         if (path.EndsWith("/heartbeat", StringComparison.Ordinal))
         {
            Heartbeats++;
            return new HttpResponseMessage(HttpStatusCode.OK);
         }

         _requests.Enqueue($"{request.Method} {path}");

         if (request.Method == HttpMethod.Post && path.EndsWith("/api/v1/translations/declaration", StringComparison.Ordinal))
         {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            DeclarationRawBodies.Enqueue(body);
            Declarations.Enqueue(JsonSerializer.Deserialize<DeclarationBody>(body, JsonOptions) ?? new DeclarationBody());

            if (DeclarationException is not null)
               throw DeclarationException;

            return new HttpResponseMessage(DeclarationStatusCode);
         }

         if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/translations/", StringComparison.Ordinal))
         {
            // Single-key: /api/v1/translations/{origin}/{locale}/{key}[/{environment}] — origin contains ':'.
            // Locale: /api/v1/translations/{locale}[/{environment}].
            if (path.Contains("%3A", StringComparison.OrdinalIgnoreCase) || path.Contains(':', StringComparison.Ordinal))
            {
               SingleKeyGets++;
               return new HttpResponseMessage(HttpStatusCode.OK)
               {
                  Content = new StringContent($$"""{"origin":"{{ORIGIN_A}}","key":"Button.Save","value":null}""", System.Text.Encoding.UTF8, "application/json")
               };
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var locale = Uri.UnescapeDataString(segments[3]);

            OnLocaleRequest?.Invoke();

            if (LocaleStatusCode != HttpStatusCode.OK)
               return new HttpResponseMessage(LocaleStatusCode);

            LocaleResponses.TryGetValue(locale, out var items);
            items ??= [];
            var json = JsonSerializer.Serialize(items, JsonOptions);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
               Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
         }

         return new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
         };
      }
   }
}
