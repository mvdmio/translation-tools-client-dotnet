using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class HeartbeatAndEnvironmentIntegrationTests
{
   private const string ProjectOriginPrefix = "mvdmio.TranslationTools.Client.Tests.Integration:";

   [Fact]
   public async Task EnvironmentSegment_ShouldBeAppendedToLocalePull_WhenConfigured()
   {
      await using var server = await StartServerAsync();

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.Environment = "production";
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      server.LocaleRequestCount.Should().Be(1);
      server.LastEnvironment.Should().Be("production");
   }

   [Fact]
   public async Task EnvironmentSegment_ShouldBeOmittedFromLocalePull_WhenNotConfigured()
   {
      await using var server = await StartServerAsync();

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

      server.LocaleRequestCount.Should().Be(1);
      server.LastEnvironment.Should().BeNull();
   }

   [Fact]
   public async Task Heartbeat_ShouldFireOnInit_AndAgainAfterInterval()
   {
      await using var server = await StartServerAsync();

      var time = new FakeTimeProvider();

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddSingleton<TimeProvider>(time);
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = true;
         options.Environment = "staging";
         options.HeartbeatInterval = TimeSpan.FromHours(1);
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      await Eventually.AssertAsync(
         () => Task.FromResult(server.HeartbeatRequestCount),
         count => count == 1,
         TimeSpan.FromSeconds(5),
         "a heartbeat should fire once on init"
      );

      server.LastHeartbeatAuthorizationHeader.Should().Be("test-api-key");
      server.LastHeartbeatBody.Should().NotBeNull();
      Guid.TryParse(server.LastHeartbeatBody!.ClientId, out _).Should().BeTrue();
      server.LastHeartbeatBody.Environment.Should().Be("staging");
      server.LastHeartbeatBody.Platform.Should().Be("dotnet");
      server.LastHeartbeatBody.Version.Should().NotBeNullOrWhiteSpace();

      // The background loop may not yet be parked on the timer; advance repeatedly until the next tick lands.
      await Eventually.AssertAsync(
         () =>
         {
            time.Advance(TimeSpan.FromHours(1));
            return Task.FromResult(server.HeartbeatRequestCount);
         },
         count => count >= 2,
         TimeSpan.FromSeconds(5),
         "a heartbeat should fire again after the interval elapses"
      );
   }

   [Fact]
   public async Task Heartbeat_FailingEndpoint_ShouldNotBreakInit()
   {
      await using var server = await StartServerAsync();
      server.HeartbeatStatusCode = 500;

      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = true;
         options.BaseUrlOverride = server.BaseUrl;
      });

      await using var app = builder.Build();

      var init = async () => await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);
      await init.Should().NotThrowAsync();

      await Eventually.AssertAsync(
         () => Task.FromResult(server.HeartbeatRequestCount),
         count => count >= 1,
         TimeSpan.FromSeconds(5),
         "a failing heartbeat should still be attempted on init without breaking startup"
      );
   }

   private static Task<TranslationToolsIntegrationTestHost> StartServerAsync()
   {
      return TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = new Dictionary<TranslationRef, string?>
            {
               [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save"
            }
         },
         TestContext.Current.CancellationToken
      );
   }
}
