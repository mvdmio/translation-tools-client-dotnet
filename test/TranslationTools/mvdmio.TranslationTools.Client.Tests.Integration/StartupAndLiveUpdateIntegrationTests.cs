using AwesomeAssertions;
using Fixture.App;
using Fixture.App.Resources.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class StartupAndLiveUpdateIntegrationTests
{
   [Fact]
   public async Task InitializeTranslationToolsClientAsync_ShouldHydrateAllConfiguredLocales_AndAllowLocaleSpecificReads()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef("/Localizations.resx", "Button.Save")] = "Save",
               [new TranslationRef("/Resources/Shared/Errors.resx", "404.title")] = "Not found"
            },
            ["nl"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef("/Localizations.resx", "Button.Save")] = "Opslaan",
               [new TranslationRef("/Resources/Shared/Errors.resx", "404.title")] = "Niet gevonden"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en"), new CultureInfo("nl")];
         options.EnableLiveUpdates = false;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();

      var previousCulture = CultureInfo.CurrentUICulture;

      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo("en");

         await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

         var client = app.Services.GetRequiredService<TranslationToolsClient>();
         var english = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);
         var dutch = await client.GetLocaleAsync(new CultureInfo("nl"), TestContext.Current.CancellationToken);

         server.LocaleRequestCount.Should().Be(2);
         server.SocketTokenRequestCount.Should().Be(0);
         server.LastAuthorizationHeader.Should().Be("test-api-key");

         english.Values[Localizations.Keys.Button_Save].Should().Be("Save");
         english.Values[Errors.Keys._404_title].Should().Be("Not found");

         dutch.Values[Localizations.Keys.Button_Save].Should().Be("Opslaan");
         dutch.Values[Errors.Keys._404_title].Should().Be("Niet gevonden");

         Localizations.Button_Save.Should().Be("Save");
         (await Localizations.GetAsync("Button.Save", new CultureInfo("nl"), cancellationToken: TestContext.Current.CancellationToken)).Should().Be("Opslaan");
         (await Errors.GetAsync("404.title", new CultureInfo("en"), cancellationToken: TestContext.Current.CancellationToken)).Should().Be("Not found");
         (await Errors.GetAsync("404.title", new CultureInfo("nl"), cancellationToken: TestContext.Current.CancellationToken)).Should().Be("Niet gevonden");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_ShouldHydrateCache_AndApplyLiveUpdatesToGeneratedAccessors()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef("/Localizations.resx", "Button.Save")] = "Save from API",
               [new TranslationRef("/Resources/Shared/Errors.resx", "404.title")] = "Not found from API"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = true;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();

      var previousCulture = CultureInfo.CurrentUICulture;

      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo("en");

         await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

         var client = app.Services.GetRequiredService<TranslationToolsClient>();
         var locale = await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);
         var socketTokenRequestCount = await Eventually.AssertAsync(
            () => Task.FromResult(server.SocketTokenRequestCount),
            count => count == 1,
            TimeSpan.FromSeconds(5),
            "live updates should request a socket token during startup"
         );

         server.LocaleRequestCount.Should().Be(1);
         socketTokenRequestCount.Should().Be(1);
         server.LastAuthorizationHeader.Should().Be("test-api-key");

         locale.Values[Localizations.Keys.Button_Save].Should().Be("Save from API");
         locale.Values[Errors.Keys._404_title].Should().Be("Not found from API");
         Localizations.Button_Save.Should().Be("Save from API");
         (await Errors.GetAsync("404.title", new CultureInfo("en"), cancellationToken: TestContext.Current.CancellationToken)).Should().Be("Not found from API");

         var fixtureResult = await FixtureUsage.ExerciseAsync(app.Services);
         fixtureResult.SyncValue.Should().Be("Save from API");
         fixtureResult.AsyncValue.Should().Be("Not found from API");

         await server.SendLiveUpdateAsync(
            """{"type":"translation-updated","origin":"/Localizations.resx","locale":"en","key":"Button.Save","value":"Save live"}""",
            TestContext.Current.CancellationToken
         );

         var updated = await Eventually.AssertAsync(
            () => Task.FromResult(client.TryGetCached(Localizations.Keys.Button_Save, new CultureInfo("en"))?.Value),
            value => value == "Save live",
            TimeSpan.FromSeconds(5),
            "live update should reach the client cache"
         );

         updated.Should().Be("Save live");
         Localizations.Button_Save.Should().Be("Save live");

         var updatedLocale = await Eventually.AssertAsync(
            () => client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken),
            snapshot => snapshot.Values.TryGetValue(Localizations.Keys.Button_Save, out var value) && value == "Save live",
            TimeSpan.FromSeconds(5),
            "live update should refresh the cached locale snapshot"
         );

         updatedLocale.Values[Localizations.Keys.Button_Save].Should().Be("Save live");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }
}
