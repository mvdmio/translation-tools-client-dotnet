using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class LocaleResolutionTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public void EffectiveLocale_Resolve_FromName_DoesNotRequireRuntimeCulture()
   {
      var locale = EffectiveLocale.Resolve("zz-x-notreal", "en");

      locale.Name.Should().Be("zz-x-notreal");
   }

   [Fact]
   public async Task Lookup_UnderInvariantCulture_ShouldRequestPathForDefaultLocale()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, defaultLocale: "en");

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

      handler.LastGetPath.Should().Be(ExpectedPath(translation, "en"));
      handler.RequestCount.Should().Be(1);
   }

   [Fact]
   public async Task Lookup_UnderInvariantCultureAndDefaultLocale_ShouldShareOneCacheEntry()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, defaultLocale: "en");

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
      handler.RequestCount.Should().Be(1);

      await client.GetAsync(translation, new CultureInfo("en"), TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(1);
   }

   [Fact]
   public async Task Lookup_ForNamedLocale_ShouldRequestAsNamed_AndMissDoesNotFollowUpForParentLocale()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, defaultLocale: "en");

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.GetAsync(translation, new CultureInfo("nl-BE"), TestContext.Current.CancellationToken);

      handler.LastGetPath.Should().Be(ExpectedPath(translation, "nl-BE"));
      handler.RequestCount.Should().Be(1);
   }

   [Fact]
   public async Task CacheWrite_UnderInvariantCulture_ShouldLandInTheDefaultLocaleBucket()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, defaultLocale: "en");

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      // Every write path resolves the effective locale too, so a cache entry written under the
      // invariant culture is readable under the default locale and satisfies its lookups.
      await client.ApplyUpdateAsync(translation, "Save now", CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

      client.TryGetCached(translation, new CultureInfo("en"))!.Value.Should().Be("Save now");

      await client.GetAsync(translation, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

      handler.RequestCount.Should().Be(0);
   }

   private static string ExpectedPath(TranslationRef translation, string locale)
   {
      return $"/api/v1/translations/{Uri.EscapeDataString(translation.Origin)}/{Uri.EscapeDataString(locale)}/{Uri.EscapeDataString(translation.Key)}";
   }

   private static TranslationToolsClient CreateClient(RecordingHandler handler, string defaultLocale)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = defaultLocale
         }),
         new LocalTranslationToolsClientCache()
      );
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      public string? LastGetPath { get; private set; }

      public int RequestCount { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         RequestCount++;
         LastGetPath = request.RequestUri?.AbsolutePath;

         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent(
               """{"origin":"Fixture.App:/Localizations.resx","key":"Button.Save","value":"Save","fallbackValue":null,"locale":"en"}""",
               System.Text.Encoding.UTF8,
               "application/json"
            )
         });
      }
   }
}
