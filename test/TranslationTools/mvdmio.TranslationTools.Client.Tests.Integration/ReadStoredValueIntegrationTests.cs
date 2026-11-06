using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.Tests.Integration._Fixture;
using System.Globalization;
using System.Net.Http;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ReadStoredValueIntegrationTests
{
   private const string RuntimeOrigin = "Jewel.Unified.staging:/WorkProcessTemplates.resx";
   private const string KeyName = "WorkProcessTemplate.3f2a0c1e-5b7d-4e8a-9c11-2d6f0a7b9e44.Name";

   private static readonly TranslationRef TemplateName = new(RuntimeOrigin, KeyName);
   private static readonly CultureInfo Dutch = new("nl");

   [Fact]
   public async Task GetStoredValueAsync_WithStoredValue_ReturnsItUntrimmed()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "  Intake NL \n";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var stored = await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      stored.Should().Be("  Intake NL \n");
   }

   [Theory]
   [InlineData("")]
   [InlineData("   ")]
   [InlineData(null)]
   public async Task GetStoredValueAsync_WithBlankOrNullValue_ReturnsNull(string? answer)
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => answer;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var stored = await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      stored.Should().BeNull();
   }

   [Fact]
   public async Task GetStoredValueAsync_SendsTheKeyRouteWithEnvironment_AndNoDefaultOrLocaleValues()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake NL";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      server.SingleKeyRequests.Should().ContainSingle().Which.Should().Be(
         new TranslationToolsIntegrationTestHost.SingleKeyRequest(RuntimeOrigin, "nl", KeyName, "staging", Query: "")
      );
   }

   [Fact]
   public async Task GetStoredValueAsync_WithBlankLocale_ReadsTheDefaultLocale()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake";
      await using var app = BuildApp(server, environment: null);
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.GetStoredValueAsync(TemplateName, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

      server.SingleKeyRequests.Should().ContainSingle().Which.Locale.Should().Be("en");
   }

   [Fact]
   public async Task GetStoredValueAsync_CalledTwice_AsksTheServiceTwice()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake NL";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);
      await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      server.SingleKeyRequests.Should().HaveCount(2);
   }

   [Fact]
   public async Task GetStoredValueAsync_InALocaleWithNoRow_ReturnsNullNotAnotherLocalesText()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = request => request.Locale == "en" ? "Intake" : null;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      (await client.GetStoredValueAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Should().Be("Intake");
      (await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Should().BeNull();
   }

   [Fact]
   public async Task GetStoredValueAsync_LeavesTheCacheAsItWas()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake NL";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);
      server.SingleKeyRequests.Should().HaveCount(1);

      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().Be("Intake NL");
      server.SingleKeyRequests.Should().HaveCount(2);
   }

   [Fact]
   public async Task GetStoredValueAsync_IgnoresTheCache()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake NL";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().Be("Intake NL");
      server.TranslationAnswer = _ => "Edited elsewhere";

      var stored = await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      stored.Should().Be("Edited elsewhere");
      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().Be("Intake NL");
   }

   [Fact]
   public async Task GetStoredValueAsync_WithFailingStatus_Throws_WhileGetAsyncStillAnswers()
   {
      await using var server = await StartServerAsync();
      server.TranslationStatusCode = StatusCodes.Status500InternalServerError;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*500*");
      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().BeNull();
   }

   [Fact]
   public async Task GetStoredValueAsync_WithNotFoundStatus_Throws()
   {
      await using var server = await StartServerAsync();
      server.TranslationStatusCode = StatusCodes.Status404NotFound;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*404*");
   }

   [Fact]
   public async Task GetStoredValueAsync_WhenServiceIsUnreachable_Throws_WhileGetAsyncStillAnswers()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging", baseUrl: "http://127.0.0.1:1");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>();
      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().BeNull();
   }

   [Fact]
   public async Task GetStoredValueAsync_WhenTheServiceAnswersTooSlowly_ThrowsTheCouldNotBeReachedError_WhileGetAsyncStillAnswers()
   {
      await using var server = await StartServerAsync();
      server.TranslationDelay = new TaskCompletionSource().Task;
      await using var app = BuildApp(server, environment: "staging", lookupTimeout: TimeSpan.FromMilliseconds(100));
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<TranslationLookupException>().WithMessage("*could not be reached*");
      (await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken)).Value.Should().BeNull();
   }

   [Fact]
   public async Task GetStoredValueAsync_WithCancelledToken_ThrowsOperationCanceled()
   {
      await using var server = await StartServerAsync();
      server.TranslationAnswer = _ => "Intake NL";
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      using var cancelled = new CancellationTokenSource();
      await cancelled.CancelAsync();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, cancelled.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
   }

   [Fact]
   public async Task GetStoredValueAsync_AfterALookupOpenedTheSuppressionWindow_StillAsksTheService()
   {
      await using var server = await StartServerAsync();
      server.TranslationStatusCode = StatusCodes.Status500InternalServerError;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);
      server.TranslationStatusCode = StatusCodes.Status200OK;
      server.TranslationAnswer = _ => "Intake NL";

      var stored = await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      stored.Should().Be("Intake NL");
   }

   [Fact]
   public async Task GetStoredValueAsync_WhenItFails_DoesNotSuppressTheNextLookup()
   {
      await using var server = await StartServerAsync();
      server.TranslationStatusCode = StatusCodes.Status500InternalServerError;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      var failing = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);
      await failing.Should().ThrowAsync<HttpRequestException>();
      server.TranslationStatusCode = StatusCodes.Status200OK;
      server.TranslationAnswer = _ => "Intake NL";

      var item = await client.GetAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      item.Value.Should().Be("Intake NL");
   }

   [Fact]
   public async Task GetStoredValueAsync_WithThrowOnLookupErrorOff_StillThrowsOnFailure()
   {
      await using var server = await StartServerAsync();
      server.TranslationStatusCode = StatusCodes.Status503ServiceUnavailable;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.GetStoredValueAsync(TemplateName, Dutch, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*503*");
   }

   private static async Task<TranslationToolsIntegrationTestHost> StartServerAsync()
   {
      return await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>(),
         TestContext.Current.CancellationToken
      );
   }

   private static WebApplication BuildApp(TranslationToolsIntegrationTestHost server, string? environment, string? baseUrl = null, TimeSpan? lookupTimeout = null)
   {
      var builder = WebApplication.CreateBuilder();
      builder.Services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "test-api-key";
         options.DefaultLocale = "en";
         options.SupportedLocales = [new CultureInfo("en")];
         options.EnableLiveUpdates = false;
         options.EnableHeartbeat = false;
         options.Environment = environment;
         options.BaseUrlOverride = baseUrl ?? server.BaseUrl;

         if (lookupTimeout is { } timeout)
            options.LookupTimeout = timeout;
      });

      return builder.Build();
   }
}
