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

public class InitializeMissingKeysTests
{
   private const string OriginA = "Fixture.App:/Localizations.resx";
   private const string OriginB = "Fixture.App:/Resources/Shared/Errors.resx";

   private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

   [Fact]
   public async Task Initialize_AfterSuccessfulPreload_PostsMissingKeysWithNeutralAndSiblingValues()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            OriginA,
            "Button.Save",
            neutralValue: "Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nl"] = "Opslaan" }
         )
      };

      using var client = CreateClient(handler, catalog, environment: "staging", supportedLocales: [new CultureInfo("en"), new CultureInfo("nl")]);

      await client.Initialize(TestContext.Current.CancellationToken);

      var push = handler.ProjectPushes.Should().ContainSingle().Subject;
      push.Environment.Should().Be("staging");
      push.Prune.Should().BeFalse();
      push.Globals.Should().BeNull();
      push.Items.Should().BeEquivalentTo(
         [
            new PushItem(OriginA, "en", "Button.Save", "Save"),
            new PushItem(OriginA, "nl", "Button.Save", "Opslaan")
         ]
      );
   }

   [Fact]
   public async Task Initialize_DoesNotPostKeyThatAppearedInAnySnapshot_EvenWhenSnapshotValueIsEmpty()
   {
      var handler = new RecordingHandler
      {
         LocaleResponses =
         {
            ["en"] =
            [
               new TranslationItemResponse { Origin = OriginA, Key = "Button.Save", Value = null },
               new TranslationItemResponse { Origin = OriginA, Key = "Only.Remote", Value = "remote" }
            ]
         }
      };

      var catalog = new[]
      {
         new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save"),
         new TranslationCatalogKey(OriginA, "Button.Cancel", neutralValue: "Cancel")
      };

      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")]);

      await client.Initialize(TestContext.Current.CancellationToken);

      var push = handler.ProjectPushes.Should().ContainSingle().Subject;
      push.Items.Should().ContainSingle()
         .Which.Should().BeEquivalentTo(new PushItem(OriginA, "en", "Button.Cancel", "Cancel"));
   }

   [Fact]
   public async Task Initialize_FailedLocaleGet_ProducesNoProjectPushAndStillThrows()
   {
      var handler = new RecordingHandler { LocaleStatusCode = HttpStatusCode.InternalServerError };
      var catalog = new[] { new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>();
      handler.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task Initialize_FailedProjectPush_DoesNotThrow_IsLogged_AndHeartbeatStillStarts()
   {
      var handler = new RecordingHandler { ProjectPushStatusCode = HttpStatusCode.InternalServerError };
      var time = new FakeTimeProvider();
      var logger = new FakeLogger();
      var catalog = new[] { new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], time: time, logger: logger);

      var act = async () => await client.Initialize(TestContext.Current.CancellationToken);
      await act.Should().NotThrowAsync();

      await WaitForHeartbeatsAsync(handler, 1);
      logger.Entries.Should().Contain(entry =>
         entry.Level == LogLevel.Warning && entry.Message.Contains("missing-key", StringComparison.OrdinalIgnoreCase)
      );
   }

   [Fact]
   public async Task Initialize_EmptyMissingSet_ProducesNoProjectPush()
   {
      var handler = new RecordingHandler
      {
         LocaleResponses =
         {
            ["en"] = [new TranslationItemResponse { Origin = OriginA, Key = "Button.Save", Value = "Save" }]
         }
      };
      var catalog = new[] { new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task Initialize_EmptyCatalog_ProducesNoProjectPush()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, catalog: [], supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task Initialize_OmitsEnvironment_WhenUnnamed()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, environment: null, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushRawBodies.Should().ContainSingle()
         .Which.Should().NotContain("environment");
   }

   [Fact]
   public async Task Initialize_AfterSuccessfulSend_LookupIsAnsweredFromCacheWithoutSingleKeyGet()
   {
      var handler = new RecordingHandler();
      var catalog = new[] { new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save") };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);
      handler.SingleKeyGets.Should().Be(0);

      var result = await client.GetAsync(new TranslationRef(OriginA, "Button.Save"), new CultureInfo("en"), TestContext.Current.CancellationToken);

      result.Value.Should().Be("Save");
      handler.SingleKeyGets.Should().Be(0);
   }

   [Fact]
   public async Task Initialize_SecondCall_SendsNothingWhenKeysAreAlreadyOnTheService()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(
            OriginA,
            "Button.Save",
            neutralValue: "Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nl"] = "Opslaan" }
         )
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en"), new CultureInfo("nl")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);
      handler.ProjectPushes.Should().ContainSingle();

      // Mimic the service now holding the keys that were just pushed.
      handler.LocaleResponses["en"] =
      [
         new TranslationItemResponse { Origin = OriginA, Key = "Button.Save", Value = "Save" }
      ];
      handler.LocaleResponses["nl"] =
      [
         new TranslationItemResponse { Origin = OriginA, Key = "Button.Save", Value = "Opslaan" }
      ];

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushes.Should().ContainSingle();
   }

   [Fact]
   public async Task Initialize_TwoOriginsSharingKeyName_BothAppearWhenMissing()
   {
      var handler = new RecordingHandler();
      var catalog = new[]
      {
         new TranslationCatalogKey(OriginA, "Button.Save", neutralValue: "Save A"),
         new TranslationCatalogKey(OriginB, "Button.Save", neutralValue: "Save B")
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushes.Should().ContainSingle().Subject.Items.Should().BeEquivalentTo(
         [
            new PushItem(OriginA, "en", "Button.Save", "Save A"),
            new PushItem(OriginB, "en", "Button.Save", "Save B")
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
            OriginA,
            "Button.Save",
            neutralValue: "Neutral Save",
            localeValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Sibling Save" }
         )
      };
      using var client = CreateClient(handler, catalog, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.ProjectPushes.Should().ContainSingle().Subject.Items.Should().ContainSingle()
         .Which.Should().BeEquivalentTo(new PushItem(OriginA, "en", "Button.Save", "Sibling Save"));
   }

   [Fact]
   public async Task Initialize_WithoutCatalogReplacement_DoesNotPostKeysFromGlobalCatalog()
   {
      TranslationCatalog.Replace(
      [
         new TranslationCatalogKey(OriginA, "Leaked.Key", neutralValue: "Leak")
      ]);

      try
      {
         var handler = new RecordingHandler();
         using var client = CreateClient(handler, catalog: null, supportedLocales: [new CultureInfo("en")], enableHeartbeat: false);

         await client.Initialize(TestContext.Current.CancellationToken);

         handler.ProjectPushes.Should().BeEmpty();
      }
      finally
      {
         TranslationCatalog.Clear();
      }
   }

   private static TranslationToolsClient CreateClient(
      RecordingHandler handler,
      IEnumerable<TranslationCatalogKey>? catalog,
      string? environment = null,
      CultureInfo[]? supportedLocales = null,
      FakeTimeProvider? time = null,
      bool enableHeartbeat = true,
      ILogger? logger = null)
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
         new LocalTranslationToolsClientCache(),
         time ?? new FakeTimeProvider(),
         new FileClientIdStore(Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"))),
         logger,
         catalog
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

   private sealed record PushItem(string Origin, string Locale, string Key, string? Value);

   private sealed class ProjectPushBody
   {
      public PushItem[] Items { get; init; } = [];
      public string? Environment { get; init; }
      public bool Prune { get; init; }
      public string[]? Globals { get; init; }
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      public HttpStatusCode LocaleStatusCode { get; set; } = HttpStatusCode.OK;
      public HttpStatusCode ProjectPushStatusCode { get; set; } = HttpStatusCode.OK;

      public Dictionary<string, TranslationItemResponse[]> LocaleResponses { get; } = new(StringComparer.OrdinalIgnoreCase);

      public ConcurrentBag<ProjectPushBody> ProjectPushes { get; } = new();
      public ConcurrentBag<string> ProjectPushRawBodies { get; } = new();

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

         if (request.Method == HttpMethod.Post && path.EndsWith("/api/v1/translations/project", StringComparison.Ordinal))
         {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            ProjectPushRawBodies.Add(body);
            var parsed = JsonSerializer.Deserialize<ProjectPushBody>(body, JsonOptions) ?? new ProjectPushBody();
            ProjectPushes.Add(parsed);
            return new HttpResponseMessage(ProjectPushStatusCode);
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
                  Content = new StringContent("""{"origin":"x","key":"y","value":null}""", System.Text.Encoding.UTF8, "application/json")
               };
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var locale = Uri.UnescapeDataString(segments[3]);

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
