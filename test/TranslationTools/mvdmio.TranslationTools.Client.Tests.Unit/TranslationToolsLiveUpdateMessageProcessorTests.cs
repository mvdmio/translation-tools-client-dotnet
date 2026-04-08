using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsLiveUpdateMessageProcessorTests
{
   [Fact]
   public async Task ProcessAsync_ShouldApplyTranslationUpdatedPayload()
   {
      using var client = CreateClient();

      await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
         client,
         """{"type":"translation-updated","origin":"/Localizations.resx","locale":"en","key":"Button.Save","value":"Save now"}""",
         TestContext.Current.CancellationToken
      );

      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Save"), new CultureInfo("en"))!.Value.Should().Be("Save now");
   }

   [Fact]
   public async Task ProcessAsync_ShouldThrowWhenOriginIsMissing()
   {
      using var client = CreateClient();

      var act = async () => await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
         client,
         """{"type":"translation-updated","locale":"en","key":"Button.Save","value":"Save now"}""",
         TestContext.Current.CancellationToken
      );

      await act.Should().ThrowAsync<ArgumentException>();
   }

   [Fact]
   public async Task ProcessAsync_ShouldIgnoreUnknownMessageType()
   {
      using var client = CreateClient();

      await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
         client,
         """{"type":"connected"}""",
         TestContext.Current.CancellationToken
      );

      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Save"), new CultureInfo("en")).Should().BeNull();
   }

   [Fact]
   public async Task ProcessAsync_ShouldIgnoreInvalidIdentityOrLocale()
   {
      using var client = CreateClient();

      await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
         client,
         """{"type":"translation-updated","origin":"invalid","locale":"en","key":"Button.Save","value":"Save now"}""",
         TestContext.Current.CancellationToken
      );
      await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
         client,
         """{"type":"translation-updated","origin":"/Localizations.resx","locale":"bad-locale-@@","key":"Button.Save","value":"Save now"}""",
         TestContext.Current.CancellationToken
      );

      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Save"), new CultureInfo("en")).Should().BeNull();
   }

   private static TranslationToolsClient CreateClient()
   {
      return new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache()
      );
   }

   private sealed class EmptySuccessHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]")
         });
      }
   }
}
