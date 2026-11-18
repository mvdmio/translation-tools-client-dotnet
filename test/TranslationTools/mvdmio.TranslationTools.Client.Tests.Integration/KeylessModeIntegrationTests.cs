using AwesomeAssertions;
using IntegrationFixture.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class KeylessModeIntegrationTests
{
   private const string ProjectOriginPrefix = "mvdmio.TranslationTools.Client.Tests.Integration:";

   [Fact]
   public async Task KeylessApp_AnswersTheLocalFallback_AndTheServiceReceivesNothing()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save from API"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = Host.CreateApplicationBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "";
         options.AllowMissingApiKey = true;
         options.SupportedLocales = [new CultureInfo("en"), new CultureInfo("nl")];
         options.EnableLiveUpdates = true;
         options.EnableHeartbeat = true;
         options.BaseUrlOverride = server.BaseUrl;
      });

      using var host = builder.Build();

      var previousCulture = CultureInfo.CurrentUICulture;

      try
      {
         await host.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

         CultureInfo.CurrentUICulture = new CultureInfo("en");
         Localizations.Button_Save.Should().Be("Save fallback");

         CultureInfo.CurrentUICulture = new CultureInfo("nl");
         Localizations.Button_Save.Should().Be("Opslaan fallback");

         server.LocaleRequestCount.Should().Be(0);
         server.TranslationRequestCount.Should().Be(0);
         server.SocketTokenRequestCount.Should().Be(0);
         server.HeartbeatRequestCount.Should().Be(0);
         server.Declarations.Should().BeEmpty();
         server.ProjectPushes.Should().BeEmpty();
         server.LastAuthorizationHeader.Should().BeNull();
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }

   [Fact]
   public async Task AppWithARealKeyAndTheOptionOn_UsesTheService()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save from API"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = Host.CreateApplicationBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.AllowMissingApiKey = true;
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = true;
         options.EnableHeartbeat = true;
         options.BaseUrlOverride = server.BaseUrl;
      });

      using var host = builder.Build();

      var previousCulture = CultureInfo.CurrentUICulture;

      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo("en");

         await host.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

         await Eventually.AssertAsync(
            () => Task.FromResult(server.HeartbeatRequestCount),
            count => count == 1,
            TimeSpan.FromSeconds(5),
            "a client with a key should send its heartbeat"
         );

         await Eventually.AssertAsync(
            () => Task.FromResult(server.SocketTokenRequestCount),
            count => count >= 1,
            TimeSpan.FromSeconds(5),
            "a client with a key should ask for its live-update token"
         );

         server.Declarations.Should().ContainSingle();
         server.LastAuthorizationHeader.Should().Be("test-api-key");
         Localizations.Button_Save.Should().Be("Save from API");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }
}
