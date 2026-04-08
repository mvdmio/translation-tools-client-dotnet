using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientBehaviorTests
{
   [Fact]
   public async Task ApplyLocaleUpdateAsync_ShouldPopulateSnapshotAndTranslationCache()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new("/Feature/Shared.resx", "Button.Save")] = "Feature save",
            [new("/Localizations.resx", "Button.Cancel")] = "Cancel"
         },
         TestContext.Current.CancellationToken
      );

      client.TryGetCached(new TranslationRef("/Feature/Shared.resx", "Button.Save"), new CultureInfo("en"))!.Value.Should().Be("Feature save");
      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Cancel"), new CultureInfo("en"))!.Value.Should().Be("Cancel");

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Contains(new TranslationRef("/Feature/Shared.resx", "Button.Save")).Should().BeTrue();
      locale.Values[new TranslationRef("/Localizations.resx", "Button.Cancel")].Should().Be("Cancel");
   }

   [Fact]
   public async Task ApplyUpdateAsync_ShouldRefreshExistingLocaleSnapshot()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef("/Localizations.resx", "Button.Save")] = "Save"
         },
         TestContext.Current.CancellationToken
      );

      await client.ApplyUpdateAsync(
         new TranslationRef("/Localizations.resx", "Button.Save"),
         "Save now",
         new CultureInfo("en"),
         TestContext.Current.CancellationToken
      );

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Values[new TranslationRef("/Localizations.resx", "Button.Save")].Should().Be("Save now");
      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Save"), new CultureInfo("en"))!.Value.Should().Be("Save now");
   }

   [Fact]
   public async Task Invalidate_ShouldRemoveTranslationFromItemAndLocaleCache()
   {
      using var client = CreateClient();
      var translation = new TranslationRef("/Localizations.resx", "Button.Save");

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [translation] = "Save",
            [new("/Localizations.resx", "Button.Cancel")] = "Cancel"
         },
         TestContext.Current.CancellationToken
      );

      client.Invalidate(translation, new CultureInfo("en"));

      client.TryGetCached(translation, new CultureInfo("en")).Should().BeNull();
      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);
      locale.Values.ContainsKey(new TranslationRef("/Localizations.resx", "Button.Save")).Should().BeFalse();
      locale.Values.ContainsKey(new TranslationRef("/Localizations.resx", "Button.Cancel")).Should().BeTrue();
   }

   [Fact]
   public async Task InvalidateLocale_ShouldClearLocaleAndTranslationCacheEntries()
   {
      using var client = CreateClient();

      await client.ApplyLocaleUpdateAsync(
         new CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef("/Localizations.resx", "Button.Save")] = "Save"
         },
         TestContext.Current.CancellationToken
      );

      client.InvalidateLocale(new CultureInfo("en"));

      client.TryGetCached(new TranslationRef("/Localizations.resx", "Button.Save"), new CultureInfo("en")).Should().BeNull();

      var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      locale.Values.Should().BeEmpty();
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
