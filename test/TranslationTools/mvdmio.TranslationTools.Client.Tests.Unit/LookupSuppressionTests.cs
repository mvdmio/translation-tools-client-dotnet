using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// After a failure that says the service itself is unreachable or broken (a connection failure, a
/// timeout, or a 5xx), the client stops calling out for one minute and answers every single-key
/// lookup from its local fallback. A response the service actually produced about one request — a
/// 401 or a 404 — never opens the window. The window is process-wide (shared across locales and
/// keys), tracked against the injected <see cref="TimeProvider"/>, and nothing probes the service
/// in the background to close it early.
/// </summary>
public class LookupSuppressionTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public async Task ServerError_ShouldOpenWindow_NextLookupForDifferentKey_MakesNoRequest()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      var response = await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(1);
      response.Value.Should().Be("Cancel");
   }

   [Fact]
   public async Task TransportException_ShouldOpenWindow_SameAsServerError()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => throw new HttpRequestException("Simulated transport failure."));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      var response = await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(1);
      response.Value.Should().Be("Cancel");
   }

   [Fact]
   public async Task Timeout_ShouldOpenWindow_SameAsServerError()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new NeverCompletingHandler(), time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      var firstTask = client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      time.Advance(TimeSpan.FromSeconds(5));
      await firstTask;

      // The window is now open: a lookup for a different key returns immediately from the
      // local fallback without needing the clock advanced again, because no request is sent.
      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      var secondTask = client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);
      var completed = await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

      completed.Should().Be(secondTask);
      (await secondTask).Value.Should().Be("Cancel");
   }

   [Fact]
   public async Task Unauthorized_ShouldNotOpenWindow_NextLookupMakesRequest()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(2);
   }

   [Fact]
   public async Task NotFound_ShouldNotOpenWindow_NextLookupMakesRequest()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(2);
   }

   [Fact]
   public async Task AdvancingClockPastOneMinute_ShouldAllowNextLookupToCallOutAgain()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1, "the window is still open");

      time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

      var third = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Delete");
      await client.GetAsync(third, new CultureInfo("en"), defaultValue: "Delete", localeValues: null, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(2, "the window has expired, so the next lookup calls out again");
   }

   [Fact]
   public async Task Nothing_ShouldProbeInBackground_WhileWindowIsOpen()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      // Advance the clock through most of the window with no further lookups. If anything probed
      // the service in the background, the request count would move without a caller asking.
      time.Advance(TimeSpan.FromSeconds(45));
      await Task.Delay(50, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(1);
   }

   [Fact]
   public async Task SuppressedLookup_StillConsultsCacheFirst()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(index => index == 1
         ? new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent(
               """{"origin":"Fixture.App:/Localizations.resx","key":"Button.Cancel","value":"Served"}""",
               Encoding.UTF8,
               "application/json"
            )
         }
         : new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time);

      var cached = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      var served = await client.GetAsync(cached, new CultureInfo("en"), TestContext.Current.CancellationToken);
      served.Value.Should().Be("Served");
      handler.RequestCount.Should().Be(1);

      var failing = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(failing, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(2, "the window opens only after this failure");

      // The window is now open. The already-cached key is still answered from the cache, not
      // suppressed local fallback logic, and still without a request.
      var stillCached = await client.GetAsync(cached, new CultureInfo("en"), TestContext.Current.CancellationToken);
      stillCached.Value.Should().Be("Served");
      handler.RequestCount.Should().Be(2);
   }

   [Fact]
   public async Task SuppressedLookup_StillLogs()
   {
      var time = new FakeTimeProvider();
      var logger = new FakeLogger();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time, logger);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      logger.Entries.Should().HaveCount(1);

      var second = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel");
      await client.GetAsync(second, new CultureInfo("en"), defaultValue: "Cancel", localeValues: null, TestContext.Current.CancellationToken);

      logger.Entries.Should().HaveCount(2);
      logger.Entries.Should().Contain(entry => entry.Message.Contains("Button.Cancel"));
   }

   [Fact]
   public async Task WholeLocaleLookup_InsideWindow_ShouldThrow_WithoutMakingRequest()
   {
      var time = new FakeTimeProvider();
      var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
      using var client = CreateClient(handler, time);

      var first = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      await client.GetAsync(first, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var act = async () => await client.GetLocaleAsync(new CultureInfo("nl-NL"), TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<TranslationLookupException>();
      handler.RequestCount.Should().Be(1, "the whole-locale lookup must not have called out");
   }

   private static TranslationToolsClient CreateClient(HttpMessageHandler handler, TimeProvider time, ILogger? logger = null)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache(),
         time,
         logger: logger
      );
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      private readonly Func<int, HttpResponseMessage> _responder;

      public RecordingHandler(Func<int, HttpResponseMessage> responder)
      {
         _responder = responder;
      }

      public int RequestCount { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         RequestCount++;
         return Task.FromResult(_responder(RequestCount));
      }
   }

   private sealed class NeverCompletingHandler : HttpMessageHandler
   {
      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         await Task.Delay(Timeout.Infinite, cancellationToken);
         throw new InvalidOperationException("Unreachable: the delay above should have been cancelled.");
      }
   }
}
