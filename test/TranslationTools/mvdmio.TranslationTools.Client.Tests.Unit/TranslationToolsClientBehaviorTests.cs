using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientBehaviorTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public async Task ApplyLocaleUpdateAsync_ShouldPopulateSnapshotAndTranslationCache()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new(ProjectOriginPrefix + "/Feature/Shared.resx", "Button.Save")] = "Feature save",
            [new(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")] = "Cancel"
         },
         TestContext.Current.CancellationToken
      );

      client.TryGetCached(new TranslationRef(ProjectOriginPrefix + "/Feature/Shared.resx", "Button.Save"), new CultureInfo("en"))!.Value.Should().Be("Feature save");
      client.TryGetCached(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel"), new CultureInfo("en"))!.Value.Should().Be("Cancel");

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Contains(new TranslationRef(ProjectOriginPrefix + "/Feature/Shared.resx", "Button.Save")).Should().BeTrue();
      locale.Values[new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")].Should().Be("Cancel");
   }

   [Fact]
   public async Task ApplyUpdateAsync_ShouldRefreshExistingLocaleSnapshot()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save"
         },
         TestContext.Current.CancellationToken
      );

      await client.ApplyUpdateAsync(
         new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save"),
         "Save now",
         new CultureInfo("en"),
         TestContext.Current.CancellationToken
      );

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Values[new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")].Should().Be("Save now");
      client.TryGetCached(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save"), new CultureInfo("en"))!.Value.Should().Be("Save now");
   }

   [Fact]
   public async Task Invalidate_ShouldRemoveTranslationFromItemAndLocaleCache()
   {
      using var client = CreateClient();
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [translation] = "Save",
            [new(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")] = "Cancel"
         },
         TestContext.Current.CancellationToken
      );

      client.Invalidate(translation, new CultureInfo("en"));

      client.TryGetCached(translation, new CultureInfo("en")).Should().BeNull();
      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);
      locale.Values.ContainsKey(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")).Should().BeFalse();
      locale.Values.ContainsKey(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")).Should().BeTrue();
   }

   [Fact]
   public async Task InvalidateLocale_ShouldClearLocaleAndTranslationCacheEntries()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save"
         },
         TestContext.Current.CancellationToken
      );

      client.InvalidateLocale(new CultureInfo("en"));

      client.TryGetCached(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save"), new CultureInfo("en")).Should().BeNull();

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Values.Should().BeEmpty();
   }

   [Fact]
   public async Task GetAsync_ShouldIncludeLocaleValuesQueryStringWhenProvided()
   {
      var capturingHandler = new CapturingHandler("""{"origin":"Fixture.App:/Localizations.resx","key":"Button.Save","value":"Save","fallbackValue":null,"locale":"en"}""");
      using var client = new TranslationToolsClient(
         new HttpClient(capturingHandler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key"
         }),
         new LocalTranslationToolsClientCache()
      );

      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      var localeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
      {
         ["en"] = "Save",
         ["nl"] = "Opslaan",
         ["de"] = "Speichern"
      };

      await client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues, TestContext.Current.CancellationToken);

      capturingHandler.LastRequestUri.Should().NotBeNull();
      var query = capturingHandler.LastRequestUri!.Query;
      query.Should().Contain("defaultValue=Save");
      query.Should().Contain("localeValues[en]=Save");
      query.Should().Contain("localeValues[nl]=Opslaan");
      query.Should().Contain("localeValues[de]=Speichern");
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

   private sealed class CapturingHandler : HttpMessageHandler
   {
      private readonly string _responseBody;

      public CapturingHandler(string responseBody)
      {
         _responseBody = responseBody;
      }

      public Uri? LastRequestUri { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         LastRequestUri = request.RequestUri;
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
         });
      }
   }
}
