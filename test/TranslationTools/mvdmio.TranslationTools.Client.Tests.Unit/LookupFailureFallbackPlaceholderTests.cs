using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using mvdmio.TranslationTools.Client.Placeholders;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

[Collection("PlaceholderRuntime")]
public sealed class LookupFailureFallbackPlaceholderTests : IDisposable
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   public LookupFailureFallbackPlaceholderTests()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   public void Dispose()
   {
      PlaceholderRuntime.Reset();
      AmbientScope.Reset();
   }

   [Fact]
   public async Task DegradedLookup_ShouldSubstitutePlaceholders_InLocalFallbackText()
   {
      using var client = new TranslationToolsClient(
         new HttpClient(new NotFoundHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache()
      );

      Translations.SetClient(client);
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Greeting");
      var localeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
      {
         ["en"] = "Hello {userName}"
      };

      var result = Translations.GetWithPlaceholders(
         translation,
         new CultureInfo("en"),
         defaultValue: null,
         localeValues,
         bindings: new Dictionary<string, string?> { ["userName"] = "World" },
         knownSet: null
      );

      result.Should().Be("Hello World");
   }

   [Fact]
   public void FluentBuilder_KeyLookedUpByString_ShouldDegradeToSuppliedNeutralValue()
   {
      using var client = new TranslationToolsClient(
         new HttpClient(new NotFoundHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache()
      );

      Translations.SetClient(client);
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var result = Translations.WithPlaceholders(translation, defaultValue: "Save").Render();

      result.Should().Be("Save");
   }

   [Fact]
   public void FluentBuilder_KeyLookedUpByString_WithNoNeutralValue_ShouldDegradeToKey()
   {
      using var client = new TranslationToolsClient(
         new HttpClient(new NotFoundHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache()
      );

      Translations.SetClient(client);
      PlaceholderRuntime.Configure(GlobalPlaceholderResolver.Empty, throwOnError: false, logger: null);

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var result = Translations.WithPlaceholders(translation).Render();

      result.Should().Be(translation.Key);
   }

   private sealed class NotFoundHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
      }
   }
}
