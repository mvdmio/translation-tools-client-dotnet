using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client.Internal;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class HeartbeatAndEnvironmentTests
{
   [Fact]
   public async Task EnvironmentSegment_ShouldBeAppendedToLocalePull_WhenConfigured()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, environment: " production ", enableHeartbeat: false, store: new ThrowingClientIdStore());

      await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      handler.LastGetPath.Should().Be("/api/v1/translations/en/production");
   }

   [Fact]
   public async Task EnvironmentSegment_ShouldBeOmittedFromLocalePull_WhenNotConfigured()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, environment: null, enableHeartbeat: false);

      await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      handler.LastGetPath.Should().Be("/api/v1/translations/en");
   }

   [Fact]
   public async Task Heartbeat_ShouldFireOnceOnInit_WithExpectedBodyAndAuthorization()
   {
      var handler = new RecordingHandler();
      var time = new FakeTimeProvider();
      using var client = CreateClient(handler, environment: "staging", time: time);

      await client.Initialize(TestContext.Current.CancellationToken);

      var heartbeat = await WaitForHeartbeatsAsync(handler, 1);
      heartbeat.Should().Be(1);

      var request = handler.Heartbeats.Single();
      request.Authorization.Should().Be("api-key");

      var body = JsonSerializer.Deserialize<HeartbeatBody>(request.Body, JsonOptions)!;
      Guid.TryParse(body.ClientId, out _).Should().BeTrue();
      body.Environment.Should().Be("staging");
      body.Platform.Should().Be("dotnet");
      body.Version.Should().NotBeNullOrWhiteSpace();
   }

   [Fact]
   public async Task Heartbeat_ShouldFireAgain_AfterIntervalElapses()
   {
      var handler = new RecordingHandler();
      var time = new FakeTimeProvider();
      using var client = CreateClient(handler, environment: null, time: time);

      await client.Initialize(TestContext.Current.CancellationToken);
      await WaitForHeartbeatsAsync(handler, 1);

      var count = await AdvanceUntilHeartbeatsAsync(handler, time, TimeSpan.FromHours(1), 2);
      count.Should().BeGreaterThanOrEqualTo(2);

      // No environment configured => environment is null in the heartbeat body.
      var body = JsonSerializer.Deserialize<HeartbeatBody>(handler.Heartbeats.First().Body, JsonOptions)!;
      body.Environment.Should().BeNull();
   }

   [Fact]
   public async Task Heartbeat_ShouldNotBeSent_WhenDisabled()
   {
      var handler = new RecordingHandler();
      var time = new FakeTimeProvider();
      using var client = CreateClient(handler, environment: null, time: time, enableHeartbeat: false);

      await client.Initialize(TestContext.Current.CancellationToken);

      await Task.Delay(200, TestContext.Current.CancellationToken);
      time.Advance(TimeSpan.FromHours(2));
      await Task.Delay(200, TestContext.Current.CancellationToken);

      handler.Heartbeats.Should().BeEmpty();
   }

   [Fact]
   public async Task Heartbeat_FailingEndpoint_ShouldNotThrowFromInit_AndShouldRetryNextTick()
   {
      var handler = new RecordingHandler { HeartbeatStatusCode = HttpStatusCode.InternalServerError };
      var time = new FakeTimeProvider();
      using var client = CreateClient(handler, environment: null, time: time);

      var init = async () => await client.Initialize(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      await WaitForHeartbeatsAsync(handler, 1);

      var count = await AdvanceUntilHeartbeatsAsync(handler, time, TimeSpan.FromHours(1), 2);
      count.Should().BeGreaterThanOrEqualTo(2);
   }

   [Fact]
   public void FileClientIdStore_ShouldReturnSameGuid_AcrossInstances()
   {
      var directory = Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"));
      try
      {
         var first = new FileClientIdStore(directory).GetOrCreateClientId();
         var second = new FileClientIdStore(directory).GetOrCreateClientId();

         first.Should().Be(second);
      }
      finally
      {
         if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
      }
   }

   [Fact]
   public async Task TwoClients_SharingStore_ShouldReportSameClientId()
   {
      var directory = Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N"));
      try
      {
         var store = new FileClientIdStore(directory);

         var handlerA = new RecordingHandler();
         var handlerB = new RecordingHandler();
         using var clientA = CreateClient(handlerA, environment: null, store: store);
         using var clientB = CreateClient(handlerB, environment: null, store: store);

         await clientA.Initialize(TestContext.Current.CancellationToken);
         await clientB.Initialize(TestContext.Current.CancellationToken);

         await WaitForHeartbeatsAsync(handlerA, 1);
         await WaitForHeartbeatsAsync(handlerB, 1);

         var idA = JsonSerializer.Deserialize<HeartbeatBody>(handlerA.Heartbeats.First().Body, JsonOptions)!.ClientId;
         var idB = JsonSerializer.Deserialize<HeartbeatBody>(handlerB.Heartbeats.First().Body, JsonOptions)!.ClientId;

         idA.Should().Be(idB);
      }
      finally
      {
         if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
      }
   }

   [Fact]
   public void FileClientIdStore_WithUnavailablePath_ShouldReturnGuid_WithoutThrowing()
   {
      // A path that points at an existing file used as a directory: writes will fail.
      var tempFile = Path.GetTempFileName();
      try
      {
         var invalidDirectory = Path.Combine(tempFile, "nested");
         var store = new FileClientIdStore(invalidDirectory);

         var act = () => store.GetOrCreateClientId();
         var id = act.Should().NotThrow().Subject;

         id.Should().NotBe(Guid.Empty);
      }
      finally
      {
         File.Delete(tempFile);
      }
   }

   private static TranslationToolsClient CreateClient(
      RecordingHandler handler,
      string? environment,
      FakeTimeProvider? time = null,
      bool enableHeartbeat = true,
      IClientIdStore? store = null)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            Environment = environment,
            EnableHeartbeat = enableHeartbeat,
            HeartbeatInterval = TimeSpan.FromHours(1)
         }),
         new LocalTranslationToolsClientCache(),
         time ?? new FakeTimeProvider(),
         store ?? new FileClientIdStore(Path.Combine(Path.GetTempPath(), "tt-client-id-" + Guid.NewGuid().ToString("N")))
      );
   }

   private static async Task<int> WaitForHeartbeatsAsync(RecordingHandler handler, int expected)
   {
      var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
      while (DateTime.UtcNow < deadline)
      {
         if (handler.Heartbeats.Count >= expected)
            return handler.Heartbeats.Count;

         await Task.Delay(25);
      }

      throw new Xunit.Sdk.XunitException($"Timed out waiting for {expected} heartbeats. Saw {handler.Heartbeats.Count}.");
   }

   private static async Task<int> AdvanceUntilHeartbeatsAsync(RecordingHandler handler, FakeTimeProvider time, TimeSpan interval, int expected)
   {
      // The background loop may not yet be parked on WaitForNextTickAsync when we advance.
      // Repeatedly advance until the expected tick is observed, so the test is not racy.
      var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
      while (DateTime.UtcNow < deadline)
      {
         if (handler.Heartbeats.Count >= expected)
            return handler.Heartbeats.Count;

         time.Advance(interval);
         await Task.Delay(25);
      }

      throw new Xunit.Sdk.XunitException($"Timed out waiting for {expected} heartbeats. Saw {handler.Heartbeats.Count}.");
   }

   private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

   private sealed record HeartbeatBody(string? ClientId, string? Environment, string? Platform, string? Version);

   private sealed record CapturedHeartbeat(string Body, string? Authorization);

   private sealed class RecordingHandler : HttpMessageHandler
   {
      public HttpStatusCode HeartbeatStatusCode { get; set; } = HttpStatusCode.OK;

      public ConcurrentBag<CapturedHeartbeat> Heartbeats { get; } = new();

      public string? LastGetPath { get; private set; }

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         var path = request.RequestUri?.AbsolutePath ?? string.Empty;

         if (path.EndsWith("/heartbeat", StringComparison.Ordinal))
         {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var auth = request.Headers.TryGetValues("Authorization", out var authValues) ? string.Join(",", authValues) : null;
            Heartbeats.Add(new CapturedHeartbeat(body, auth));
            return new HttpResponseMessage(HeartbeatStatusCode);
         }

         if (request.Method == HttpMethod.Get)
            LastGetPath = path;

         return new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]")
         };
      }
   }

   private sealed class ThrowingClientIdStore : IClientIdStore
   {
      public Guid GetOrCreateClientId() => Guid.NewGuid();
   }
}
