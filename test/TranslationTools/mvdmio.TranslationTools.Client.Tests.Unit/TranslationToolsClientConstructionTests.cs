using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Net;
using System.Net.Http;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientConstructionTests
{
   [Fact]
   public void Constructor_ShouldThrow_WhenApiKeyIsBlank()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = " "
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*ApiKey*");
   }

   [Fact]
   public void Constructor_ShouldThrow_WhenDefaultLocaleIsBlank()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = " "
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*DefaultLocale*");
   }

   [Fact]
   public void Constructor_ShouldThrow_WhenDefaultLocaleIsNotARecognisedLocale()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = "!!!"
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*DefaultLocale*");
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
