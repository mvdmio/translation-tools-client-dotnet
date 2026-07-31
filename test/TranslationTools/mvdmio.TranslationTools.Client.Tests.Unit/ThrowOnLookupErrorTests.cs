using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// With <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> set, a single-key lookup
/// rethrows each failure class instead of degrading to the local fallback. Named and shaped after
/// the existing <c>ThrowOnPlaceholderError</c>.
/// </summary>
public class ThrowOnLookupErrorTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Theory]
   [InlineData(HttpStatusCode.Unauthorized)]
   [InlineData(HttpStatusCode.NotFound)]
   [InlineData(HttpStatusCode.InternalServerError)]
   public async Task Lookup_UnsuccessfulResponse_ShouldThrow_WhenThrowOnLookupErrorIsSet(HttpStatusCode statusCode)
   {
      using var client = CreateClient(new StatusHandler(statusCode));
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<TranslationLookupException>();
   }

   [Fact]
   public async Task Lookup_TransportException_ShouldThrow_WhenThrowOnLookupErrorIsSet()
   {
      using var client = CreateClient(new ThrowingHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<TranslationLookupException>();
   }

   [Fact]
   public async Task Lookup_UndeserialisableBody_ShouldThrow_WhenThrowOnLookupErrorIsSet()
   {
      using var client = CreateClient(new BadJsonHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<TranslationLookupException>();
   }

   [Fact]
   public async Task Lookup_CallersCancellation_ShouldStillPropagate_AsCancellation_RatherThanTranslationLookupException()
   {
      using var client = CreateClient(new NeverCompletingHandler());
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      using var cts = new CancellationTokenSource();
      await cts.CancelAsync();

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, cts.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
   }

   [Theory]
   [InlineData(HttpStatusCode.Unauthorized)]
   [InlineData(HttpStatusCode.NotFound)]
   [InlineData(HttpStatusCode.InternalServerError)]
   public async Task Lookup_UnsuccessfulResponse_ShouldStillDegrade_WhenThrowOnLookupErrorIsUnset(HttpStatusCode statusCode)
   {
      using var client = CreateClient(new StatusHandler(statusCode), throwOnLookupError: false);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var act = async () => await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      var result = await act.Should().NotThrowAsync();
      result.Subject.Value.Should().Be("Save");
   }

   private static TranslationToolsClient CreateClient(HttpMessageHandler handler, bool throwOnLookupError = true)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            ThrowOnLookupError = throwOnLookupError
         }),
         new LocalTranslationToolsClientCache()
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
}
