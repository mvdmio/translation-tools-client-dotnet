using AwesomeAssertions;
using IntegrationFixture.App;
using IntegrationFixture.App.Resources.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class InitializeDeclarationIntegrationTests
{
   private const string ProjectOriginPrefix = "mvdmio.TranslationTools.Client.Tests.Integration:";
   private const string LocalizationsOrigin = ProjectOriginPrefix + "/Localizations.resx";
   private const string ErrorsOrigin = ProjectOriginPrefix + "/Resources/Shared/Errors.resx";

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_PostsDeclarationWithNeutralAndSiblingValues()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>(),
            ["nl"] = new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.DefaultLocale = "en";
         options.SupportedLocales = [new CultureInfo("en"), new CultureInfo("nl")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.Environment = "staging";
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      var declaration = server.Declarations.Should().ContainSingle().Subject;
      declaration.Environment.Should().Be("staging");
      declaration.Items.Should().BeEquivalentTo(
         [
            new { Origin = LocalizationsOrigin, Locale = "en", Key = "Button.Save", Value = "Save fallback" },
            new { Origin = LocalizationsOrigin, Locale = "nl", Key = "Button.Save", Value = "Opslaan fallback" },
            new { Origin = ErrorsOrigin, Locale = "en", Key = "404.title", Value = "Fallback not found" }
         ]
      );
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_DeclaresKeysTheServiceAlreadyReturns()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef(LocalizationsOrigin, "Button.Save")] = "Edited on service",
               [new TranslationRef(ErrorsOrigin, "404.title")] = "Not found"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.DefaultLocale = "en";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      var declaration = server.Declarations.Should().ContainSingle().Subject;
      declaration.Items.Should().Contain(item => item.Origin == LocalizationsOrigin && item.Key == "Button.Save" && item.Locale == "en" && item.Value == "Save fallback");
      declaration.Items.Should().Contain(item => item.Origin == ErrorsOrigin && item.Key == "404.title" && item.Locale == "en");
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_FailedLocaleGet_StillSentTheDeclaration_AndStartupSucceeds()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );
      server.LocaleStatusCode = StatusCodes.Status500InternalServerError;

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();

      var init = async () => await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      server.Declarations.Should().ContainSingle();
   }

   [Theory]
   [InlineData(StatusCodes.Status500InternalServerError)]
   [InlineData(StatusCodes.Status404NotFound)]
   public async Task InitializeTranslationToolsClientAsync_FailedDeclaration_DoesNotThrow_AndHeartbeatStillStarts(int statusCode)
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );
      server.DeclarationStatusCode = statusCode;

      var time = new FakeTimeProvider();
      var builder = WebApplication.CreateBuilder();
      builder.Services.AddSingleton<TimeProvider>(time);
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = true;
         options.HeartbeatInterval = TimeSpan.FromHours(1);
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();

      var init = async () => await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      await Eventually.AssertAsync(
         () => Task.FromResult(server.HeartbeatRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "heartbeat should still start after a failed Declaration"
      );
      server.LocaleRequestCount.Should().Be(1);
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_OmitsEnvironmentOnDeclarationBody_WhenUnnamed()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      var declaration = server.Declarations.Should().ContainSingle().Subject;
      declaration.Environment.Should().BeNull();
      server.DeclarationRawBodies.Should().ContainSingle().Which.Should().Contain("\"items\"")
         .And.NotContain("\"environment\"")
         .And.NotContain("\"prune\"")
         .And.NotContain("\"globals\"");
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_PostsGlobalNamesAsSeparateProjectPush()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef(LocalizationsOrigin, "Button.Save")] = "Save",
               [new TranslationRef(ErrorsOrigin, "404.title")] = "Not found"
            }
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.BaseUrlOverride = server.BaseUrl;
      });
      builder.Services.AddTranslationToolsGlobalPlaceholders<StartupGlobals>();

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      server.Declarations.Should().ContainSingle().Subject.Items.Should().NotBeEmpty();
      var globalsPush = server.ProjectPushes.Should().ContainSingle().Subject;
      globalsPush.Items.Should().BeEmpty();
      globalsPush.Globals.Should().BeEquivalentTo(["company"]);
   }

   [Fact]
   public async Task GeneratedAccessor_ForKeyThePreloadDidNotReturn_StillSeedsOnFirstLookup()
   {
      await using var server = await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.DefaultLocale = "en";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      var previousCulture = CultureInfo.CurrentUICulture;
      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo("en");

         Localizations.Button_Save.Should().Be("Save fallback");

         server.TranslationRequestCount.Should().Be(1);
         server.LastTranslationRequestQuery.Should().NotBeNullOrEmpty();
         server.LastTranslationRequestQuery.Should().Contain("defaultValue=");
         server.LastTranslationRequestQuery.Should().Contain("localeValues");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }

   private sealed class StartupGlobals
   {
      [GlobalPlaceholder("company")]
      public string Company => "Acme";
   }
}
