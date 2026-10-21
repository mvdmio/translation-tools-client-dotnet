using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class FailedStartupRequestIntegrationTests
{
   private const string ProjectOriginPrefix = "mvdmio.TranslationTools.Client.Tests.Integration:";
   private static readonly TranslationRef _saveKey = new(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_WhenEveryLocaleRequestFails_WarnsPerLocale_AndStillStartsHeartbeatAndLiveUpdates()
   {
      await using var server = await StartServerAsync();
      server.LocaleStatusCode = StatusCodes.Status502BadGateway;
      var logs = new CapturingLoggerProvider();

      await using var app = BuildApp(server, logs, enableHeartbeat: true, enableLiveUpdates: true);

      var init = async () => await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      await Eventually.AssertAsync(
         () => Task.FromResult(server.HeartbeatRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "the heartbeat should start although the locale requests failed"
      );
      await Eventually.AssertAsync(
         () => Task.FromResult(server.SocketTokenRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "live updates should start although the locale requests failed"
      );

      var warnings = logs.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(static entry => entry.Message).ToArray();
      warnings.Should().ContainSingle(message => message.Contains("locale en "));
      warnings.Should().ContainSingle(message => message.Contains("locale nl "));
      logs.Entries.Should().NotContain(entry => entry.Message.Contains("Error initializing translation tools client"));
      logs.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_WhenFirstLocaleFails_StillPreloadsTheLaterLocales()
   {
      await using var server = await StartServerAsync();
      server.LocaleStatusCodes["en"] = StatusCodes.Status502BadGateway;

      await using var app = BuildApp(server, new CapturingLoggerProvider());
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      server.LocaleRequestCount.Should().Be(2);

      // The service now fails "nl"; the lookup succeeds only when the preload cached it.
      server.LocaleStatusCodes["nl"] = StatusCodes.Status502BadGateway;
      var client = app.Services.GetRequiredService<TranslationToolsClient>();
      var dutch = await client.GetLocaleAsync(new CultureInfo("nl"), TestContext.Current.CancellationToken);

      dutch.Values[_saveKey].Should().Be("Opslaan");
      server.LocaleRequestCount.Should().Be(2);
   }

   [Fact]
   public async Task GetLocaleAsync_ForLocaleThatFailedToPreload_FetchesTheWholeLocaleOnce_AndCachesIt()
   {
      await using var server = await StartServerAsync();
      server.LocaleStatusCode = StatusCodes.Status502BadGateway;

      await using var app = BuildApp(server, new CapturingLoggerProvider());
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      server.LocaleStatusCode = StatusCodes.Status200OK;
      var requestsAfterStartup = server.LocaleRequestCount;

      var client = app.Services.GetRequiredService<TranslationToolsClient>();
      var first = await client.GetLocaleAsync(new CultureInfo("nl"), TestContext.Current.CancellationToken);
      var second = await client.GetLocaleAsync(new CultureInfo("nl"), TestContext.Current.CancellationToken);

      first.Values[_saveKey].Should().Be("Opslaan");
      second.Values[_saveKey].Should().Be("Opslaan");
      server.LocaleRequestCount.Should().Be(requestsAfterStartup + 1);
   }

   [Fact]
   public async Task GetAsync_ForKeyInLocaleThatFailedToPreload_FetchesTheKeyOnce_AndServesTheSecondLookupFromCache()
   {
      await using var server = await StartServerAsync();
      server.LocaleStatusCode = StatusCodes.Status502BadGateway;

      await using var app = BuildApp(server, new CapturingLoggerProvider());
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      server.LocaleStatusCode = StatusCodes.Status200OK;

      var client = app.Services.GetRequiredService<TranslationToolsClient>();
      var first = await client.GetAsync(_saveKey, new CultureInfo("nl"), TestContext.Current.CancellationToken);
      var second = await client.GetAsync(_saveKey, new CultureInfo("nl"), TestContext.Current.CancellationToken);

      first.Value.Should().Be("Opslaan");
      second.Value.Should().Be("Opslaan");
      server.TranslationRequestCount.Should().Be(1);
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_WhenGlobalsPushFails_WarnsAndStillStartsLiveUpdates()
   {
      await using var server = await StartServerAsync();
      server.ProjectPushStatusCode = StatusCodes.Status502BadGateway;
      var logs = new CapturingLoggerProvider();

      await using var app = BuildApp(server, logs, enableLiveUpdates: true, withGlobals: true);

      var init = async () => await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      server.ProjectPushes.Should().ContainSingle();
      await Eventually.AssertAsync(
         () => Task.FromResult(server.SocketTokenRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "live updates should start although the globals push failed"
      );

      logs.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning && entry.Category.EndsWith("ITranslationToolsClient") && entry.Message.Contains("global"));
      logs.Entries.Should().NotContain(entry => entry.Message.Contains("Error initializing translation tools client"));
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_WithWorkingService_PreloadsEveryLocaleAndLogsNoWarning()
   {
      await using var server = await StartServerAsync();
      var logs = new CapturingLoggerProvider();

      await using var app = BuildApp(server, logs, enableHeartbeat: true, enableLiveUpdates: true, withGlobals: true);
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      await Eventually.AssertAsync(
         () => Task.FromResult(server.HeartbeatRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "the heartbeat should start"
      );
      await Eventually.AssertAsync(
         () => Task.FromResult(server.SocketTokenRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "live updates should start"
      );

      server.LocaleRequestCount.Should().Be(2);
      logs.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
   }

   private static WebApplication BuildApp(
      TranslationToolsIntegrationTestHost server,
      CapturingLoggerProvider logs,
      bool enableHeartbeat = false,
      bool enableLiveUpdates = false,
      bool withGlobals = false)
   {
      var builder = WebApplication.CreateBuilder();
      builder.Logging.ClearProviders();
      builder.Logging.AddProvider(logs);
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.DefaultLocale = "en";
         options.SupportedLocales = [new CultureInfo("en"), new CultureInfo("nl")];
         options.EnableLiveUpdates = enableLiveUpdates;
         options.EnableHeartbeat = enableHeartbeat;
         options.BaseUrlOverride = server.BaseUrl;
      });

      if (withGlobals)
         builder.Services.AddTranslationToolsGlobalPlaceholders<StartupGlobals>();

      return builder.Build();
   }

   private static Task<TranslationToolsIntegrationTestHost> StartServerAsync()
   {
      return TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?> { [_saveKey] = "Save" },
            ["nl"] = new Dictionary<TranslationRef, string?> { [_saveKey] = "Opslaan" }
         },
         TestContext.Current.CancellationToken
      );
   }

   private sealed class StartupGlobals
   {
      [GlobalPlaceholder("company")]
      public string Company => "Acme";
   }
}
