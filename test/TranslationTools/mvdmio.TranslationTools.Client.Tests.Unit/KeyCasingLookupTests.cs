using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class KeyCasingLookupTests
{
   private const string Origin = "Fixture.App:/Localizations.resx";

   [Fact]
   public async Task GetAsync_AfterLocalePreloadInAnotherCasing_IsAnsweredFromCache()
   {
      var handler = new LowerCaseKeyHandler();
      using var client = CreateClient(handler);
      var locale = new CultureInfo("nl");

      await client.GetLocaleAsync(locale, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      var result = await client.GetAsync(new TranslationRef(Origin, "Action.Edit"), locale, TestContext.Current.CancellationToken);

      result.Value.Should().Be("Bewerken (locale)");
      result.Key.Should().Be("action.edit");
      handler.RequestCount.Should().Be(1);
   }

   [Fact]
   public async Task GetAsync_WhenServiceAnswersInAnotherCasing_SecondLookupIsAnsweredFromCache()
   {
      var handler = new LowerCaseKeyHandler();
      using var client = CreateClient(handler);
      var locale = new CultureInfo("nl");
      var translation = new TranslationRef(Origin, "Action.Edit");

      var first = await client.GetAsync(translation, locale, TestContext.Current.CancellationToken);
      var second = await client.GetAsync(translation, locale, TestContext.Current.CancellationToken);

      first.Value.Should().Be("Bewerken (key)");
      second.Value.Should().Be("Bewerken (key)");
      second.Key.Should().Be("action.edit");
      handler.RequestCount.Should().Be(1);
   }

   private static TranslationToolsClient CreateClient(LowerCaseKeyHandler handler)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = "en"
         }),
         new LocalTranslationToolsClientCache()
      );
   }

   /// <summary>
   /// Answers like the service: it stores the key as <c>action.edit</c>, whatever casing was asked for.
   /// </summary>
   private sealed class LowerCaseKeyHandler : HttpMessageHandler
   {
      public int RequestCount { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         RequestCount++;
         var path = request.RequestUri?.AbsolutePath ?? string.Empty;
         var isSingleKey = path.Contains("%3A", StringComparison.OrdinalIgnoreCase) || path.Contains(':', StringComparison.Ordinal);

         var json = isSingleKey
            ? $$"""{"origin":"{{Origin}}","key":"action.edit","value":"Bewerken (key)","locale":"nl"}"""
            : $$"""[{"origin":"{{Origin}}","key":"action.edit","value":"Bewerken (locale)"}]""";

         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
         });
      }
   }
}
