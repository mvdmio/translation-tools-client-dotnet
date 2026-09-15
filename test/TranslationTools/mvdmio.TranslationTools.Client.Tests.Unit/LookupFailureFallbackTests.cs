using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// A single-key lookup never throws: a missing key, an unsuccessful response, a transport
/// exception, and an undeserialisable body are all answered with the local fallback chain
/// (dictionary entry for the effective locale, then the neutral value) instead.
/// </summary>
public class LookupFailureFallbackTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public async Task Lookup_404WithLocalDictionaryEntryForEffectiveLocale_ShouldReturnThatEntry()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      var localeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
      {
         ["nl-NL"] = "Opslaan"
      };

      var response = await client.GetAsync(translation, new CultureInfo("nl-NL"), defaultValue: "Save", localeValues, TestContext.Current.CancellationToken);

      response.Value.Should().Be("Opslaan");
   }

   [Fact]
   public async Task Lookup_404WithNoDictionaryEntryForEffectiveLocale_ShouldReturnNeutralValue()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var response = await client.GetAsync(translation, new CultureInfo("nl-NL"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      response.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_404WithNeitherDictionaryEntryNorNeutralValue_ShouldReturnNullValue()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var response = await client.GetAsync(translation, new CultureInfo("nl-NL"), TestContext.Current.CancellationToken);

      // The client interface answers with a null value; the translation key is the last resort
      // one layer up, in the static Translations entry point's unchanged `?? defaultValue ?? translation.Key`.
      response.Value.Should().BeNull();
   }

   [Fact]
   public async Task Lookup_DictionaryHoldingNeutralVariant_ShouldNotSatisfyMoreSpecificLocale()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      var localeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
      {
         ["nl"] = "Opslaan"
      };

      var response = await client.GetAsync(translation, new CultureInfo("nl-NL"), defaultValue: "Save", localeValues, TestContext.Current.CancellationToken);

      response.Value.Should().Be("Save");
   }

   [Theory]
   [InlineData(HttpStatusCode.Unauthorized)]
   [InlineData(HttpStatusCode.NotFound)]
   [InlineData(HttpStatusCode.InternalServerError)]
   public async Task Lookup_UnsuccessfulResponse_ShouldReturnFallback_AndNotThrow(HttpStatusCode statusCode)
   {
      using var client = CreateClient(new StatusHandler(statusCode));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      var result = await act.Should().NotThrowAsync();
      result.Subject.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_TransportException_ShouldReturnFallback_AndNotThrow()
   {
      using var client = CreateClient(new ThrowingHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      var result = await act.Should().NotThrowAsync();
      result.Subject.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_UndeserialisableBody_ShouldReturnFallback_AndNotThrow()
   {
      using var client = CreateClient(new BadJsonHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      var result = await act.Should().NotThrowAsync();
      result.Subject.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_CallersCancellation_ShouldPropagate_RatherThanDegrade()
   {
      using var client = CreateClient(new NeverCompletingHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      using var cts = new CancellationTokenSource();
      await cts.CancelAsync();

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, cts.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
   }

   [Fact]
   public async Task GetLocaleAsync_OnFailure_ShouldThrow_RatherThanReturnEmptySnapshot()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.InternalServerError));

      var act = async () => await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>();
   }

   [Fact]
   public async Task DegradedLookup_ShouldNotBeCached_AndSubsequentSuccessfulLookupReturnsServedValue()
   {
      var handler = new FirstFailsThenSucceedsHandler();
      using var client = CreateClient(handler);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var degraded = await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);
      degraded.Value.Should().Be("Save");
      client.TryGetCached(translation, new CultureInfo("en")).Should().BeNull();

      var served = await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      served.Value.Should().Be("Served");
      handler.RequestCount.Should().Be(2);
   }

   [Fact]
   public async Task DegradedLookup_ShouldLogOneLine_NamingKeyAndEffectiveLocale()
   {
      var logger = new FakeLogger();
      using var client = CreateClient(new StatusHandler(HttpStatusCode.InternalServerError), logger);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, new CultureInfo("nl-NL"), TestContext.Current.CancellationToken);

      logger.Entries.Should().ContainSingle();
      var entry = logger.Entries.Single();
      entry.Message.Should().Contain("Button.Save");
      entry.Message.Should().Contain("nl-NL");
   }

   [Fact]
   public async Task DegradedLookup_NotFound_ShouldMentionMissingValue_RatherThanUnreachable()
   {
      var logger = new FakeLogger();
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound), logger);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, new CultureInfo("en"), TestContext.Current.CancellationToken);

      var entry = logger.Entries.Single();
      entry.Level.Should().Be(LogLevel.Warning);
      entry.Message.Should().Contain("no value for this key");
   }

   [Fact]
   public async Task DegradedLookup_ServerError_ShouldSayServiceCouldNotAnswer()
   {
      var logger = new FakeLogger();
      using var client = CreateClient(new StatusHandler(HttpStatusCode.InternalServerError), logger);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, new CultureInfo("en"), TestContext.Current.CancellationToken);

      var entry = logger.Entries.Single();
      entry.Level.Should().Be(LogLevel.Warning);
      entry.Message.Should().Contain("could not answer");
   }

   [Fact]
   public async Task DegradedLookup_UnauthorizedResponse_ShouldLogAtError()
   {
      var logger = new FakeLogger();
      using var client = CreateClient(new StatusHandler(HttpStatusCode.Unauthorized), logger);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, new CultureInfo("en"), TestContext.Current.CancellationToken);

      var entry = logger.Entries.Single();
      entry.Level.Should().Be(LogLevel.Error);
   }

   [Fact]
   public async Task DegradedLookup_WithNoLoggerSupplied_ShouldStillReturnFallback()
   {
      using var client = CreateClient(new StatusHandler(HttpStatusCode.NotFound));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var response = await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      response.Value.Should().Be("Save");
   }

   private static TranslationToolsClient CreateClient(HttpMessageHandler handler, ILogger? logger = null)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache(),
         logger: logger
      );
   }

   private sealed class StatusHandler : HttpMessageHandler
   {
      private readonly HttpStatusCode _statusCode;

      public StatusHandler(HttpStatusCode statusCode)
      {
         _statusCode = statusCode;
      }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(_statusCode));
      }
   }

   private sealed class ThrowingHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         throw new HttpRequestException("Simulated transport failure.");
      }
   }

   private sealed class BadJsonHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("{not-valid-json", Encoding.UTF8, "application/json")
         });
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

   private sealed class FirstFailsThenSucceedsHandler : HttpMessageHandler
   {
      private bool _first = true;

      public int RequestCount { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         RequestCount++;

         if (_first)
         {
            _first = false;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
         }

         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent(
               """{"origin":"Fixture.App:/Localizations.resx","key":"Button.Save","value":"Served"}""",
               Encoding.UTF8,
               "application/json"
            )
         });
      }
   }
}
