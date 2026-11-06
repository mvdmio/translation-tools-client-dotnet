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
public sealed class RuntimeKeysIntegrationTests
{
   private const string RuntimeOrigin = "Jewel.Unified.staging:/WorkProcessTemplates.resx";
   private const string KeyName = "WorkProcessTemplate.3f2a0c1e-5b7d-4e8a-9c11-2d6f0a7b9e44.Name";
   private const string OtherKeyName = "WorkProcessTemplate.7c1d4e92-0a3b-4f56-8d17-9b2e5c6a1f03.Name";

   private static readonly TranslationRef TemplateName = new(RuntimeOrigin, KeyName);
   private static readonly TranslationRef OtherTemplateName = new(RuntimeOrigin, OtherKeyName);

   [Fact]
   public async Task DeclareAsync_PostsOneItemPerKeyAndLocaleWithEnvironment_AndNoPush()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.DeclareAsync(
         [
            new TranslationCatalogKey(RuntimeOrigin, KeyName, "Intake", new Dictionary<string, string> { ["nl"] = "Intake NL" }),
            new TranslationCatalogKey(RuntimeOrigin, OtherKeyName)
         ],
         TestContext.Current.CancellationToken
      );

      var declaration = server.Declarations.Should().ContainSingle().Subject;
      declaration.Environment.Should().Be("staging");
      declaration.Items.Should().BeEquivalentTo(
         [
            new { Origin = RuntimeOrigin, Locale = "en", Key = KeyName, Value = "Intake" },
            new { Origin = RuntimeOrigin, Locale = "nl", Key = KeyName, Value = "Intake NL" },
            new { Origin = RuntimeOrigin, Locale = "en", Key = OtherKeyName, Value = "" }
         ]
      );
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task DeclareAsync_WithUnnamedEnvironment_OmitsEnvironmentFromBody()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: null);
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.DeclareAsync([new TranslationCatalogKey(RuntimeOrigin, KeyName)], TestContext.Current.CancellationToken);

      server.DeclarationRawBodies.Should().ContainSingle().Which.Should().Contain("\"items\"")
         .And.NotContain("\"environment\"");
   }

   [Fact]
   public async Task DeclareAsync_WithEmptySet_SendsNothing()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.DeclareAsync([], TestContext.Current.CancellationToken);

      server.Declarations.Should().BeEmpty();
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task DeclareAsync_WithNullKeys_ThrowsBeforeAnyRequest()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.DeclareAsync(null!, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("keys");
      server.Declarations.Should().BeEmpty();
   }

   [Fact]
   public async Task DeclareAsync_WhenServiceFails_Throws()
   {
      await using var server = await StartServerAsync();
      server.DeclarationStatusCode = StatusCodes.Status500InternalServerError;
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.DeclareAsync([new TranslationCatalogKey(RuntimeOrigin, KeyName)], TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*500*");
   }

   [Fact]
   public async Task DeclareAsync_WithCancelledToken_Throws()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      using var cancelled = new CancellationTokenSource();
      await cancelled.CancelAsync();

      var act = async () => await client.DeclareAsync([new TranslationCatalogKey(RuntimeOrigin, KeyName)], cancelled.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
      server.Declarations.Should().BeEmpty();
   }

   [Fact]
   public async Task DeclareAsync_KeepsTheKeyOutOfTheStartupDeclaration()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.DeclareAsync([new TranslationCatalogKey(RuntimeOrigin, KeyName, "Intake")], TestContext.Current.CancellationToken);
      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      server.Declarations.Should().HaveCount(2);
      server.Declarations.Count(declaration => declaration.Items.Any(item => item.Origin == RuntimeOrigin)).Should().Be(1);
   }

   [Fact]
   public async Task GetAsync_ForKeyUnknownToCache_AfterDeclare_IsAnsweredByTheService()
   {
      await using var server = await StartServerAsync(
         new Dictionary<TranslationRef, string?> { [TemplateName] = "Intake" }
      );
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.DeclareAsync([new TranslationCatalogKey(RuntimeOrigin, KeyName, "Intake")], TestContext.Current.CancellationToken);
      var answer = await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken);

      answer.Value.Should().Be("Intake");
      server.SingleKeyRequests.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Origin = RuntimeOrigin, Locale = "en", Key = KeyName, Environment = "staging" });
   }

   [Fact]
   public async Task WriteAsync_PostsItemsOnlyWithEnvironment_AndTheEffectiveLocaleName()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.WriteAsync(
         [
            new TranslationValueWrite(TemplateName, new CultureInfo("nl"), "Opname"),
            new TranslationValueWrite(OtherTemplateName, CultureInfo.InvariantCulture, "New")
         ],
         TestContext.Current.CancellationToken
      );

      var push = server.ProjectPushes.Should().ContainSingle().Subject;
      push.Environment.Should().Be("staging");
      push.Items.Should().BeEquivalentTo(
         [
            new { Origin = RuntimeOrigin, Locale = "nl", Key = KeyName, Value = "Opname" },
            new { Origin = RuntimeOrigin, Locale = "en", Key = OtherKeyName, Value = "New" }
         ]
      );
      server.ProjectPushRawBodies.Should().ContainSingle().Which.Should().NotContain("\"globals\"");
      server.Declarations.Should().BeEmpty();
   }

   [Fact]
   public async Task WriteAsync_WithNullValue_ThrowsNamingTheKey_AndSendsNothing()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.WriteAsync(
         [
            new TranslationValueWrite(TemplateName, new CultureInfo("en"), "Intake v2"),
            new TranslationValueWrite(OtherTemplateName, new CultureInfo("en"), null!)
         ],
         TestContext.Current.CancellationToken
      );

      var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;
      exception.Message.Should().Contain("value to write").And.Contain("is null").And.Contain(OtherKeyName).And.NotContain(KeyName);
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task WriteAsync_WithNullLocale_ThrowsBeforeAnyRequest()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.WriteAsync([new TranslationValueWrite(TemplateName, null!, "Intake")], TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<ArgumentNullException>();
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task WriteAsync_WithNullValues_ThrowsBeforeAnyRequest()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      var act = async () => await client.WriteAsync(null!, TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("values");
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task WriteAsync_WithEmptyString_SendsItAndCachesIt()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.WriteAsync([new TranslationValueWrite(TemplateName, new CultureInfo("en"), "")], TestContext.Current.CancellationToken);

      server.ProjectPushes.Should().ContainSingle().Which.Items.Should().ContainSingle()
         .Which.Should().BeEquivalentTo(new { Origin = RuntimeOrigin, Locale = "en", Key = KeyName, Value = "" });
      (await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("");
      server.TranslationRequestCount.Should().Be(0);
   }

   [Fact]
   public async Task WriteAsync_WithEmptySet_SendsNothing()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();

      await client.WriteAsync([], TestContext.Current.CancellationToken);

      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task WriteAsync_WhenServiceFails_Throws_AndLeavesTheCacheAsItWas()
   {
      await using var server = await StartServerAsync(
         new Dictionary<TranslationRef, string?> { [TemplateName] = "Intake" }
      );
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      (await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("Intake");
      server.ProjectPushStatusCode = StatusCodes.Status500InternalServerError;

      var act = async () => await client.WriteAsync([new TranslationValueWrite(TemplateName, new CultureInfo("en"), "Intake v2")], TestContext.Current.CancellationToken);

      await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*500*");
      (await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("Intake");
      server.TranslationRequestCount.Should().Be(1);
   }

   [Fact]
   public async Task WriteAsync_WithCancelledToken_Throws()
   {
      await using var server = await StartServerAsync();
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      using var cancelled = new CancellationTokenSource();
      await cancelled.CancelAsync();

      var act = async () => await client.WriteAsync([new TranslationValueWrite(TemplateName, new CultureInfo("en"), "Intake")], cancelled.Token);

      await act.Should().ThrowAsync<OperationCanceledException>();
      server.ProjectPushes.Should().BeEmpty();
   }

   [Fact]
   public async Task GetAsync_AfterSuccessfulWrite_AnswersTheWrittenValueWithNoRequest()
   {
      await using var server = await StartServerAsync(
         new Dictionary<TranslationRef, string?> { [TemplateName] = "Intake" }
      );
      await using var app = BuildApp(server, environment: "staging");
      var client = app.Services.GetRequiredService<ITranslationToolsClient>();
      (await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("Intake");

      await client.WriteAsync(
         [
            new TranslationValueWrite(TemplateName, new CultureInfo("en"), "Intake v3"),
            new TranslationValueWrite(OtherTemplateName, CultureInfo.InvariantCulture, "New")
         ],
         TestContext.Current.CancellationToken
      );

      (await client.GetAsync(TemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("Intake v3");
      (await client.GetAsync(OtherTemplateName, new CultureInfo("en"), TestContext.Current.CancellationToken)).Value.Should().Be("New");
      server.TranslationRequestCount.Should().Be(1);
   }

   private static async Task<TranslationToolsIntegrationTestHost> StartServerAsync(Dictionary<TranslationRef, string?>? english = null)
   {
      return await TranslationToolsIntegrationTestHost.StartAsync(
         new Dictionary<string, IReadOnlyDictionary<TranslationRef, string?>>
         {
            ["en"] = english ?? new Dictionary<TranslationRef, string?>()
         },
         TestContext.Current.CancellationToken
      );
   }

   private static WebApplication BuildApp(TranslationToolsIntegrationTestHost server, string? environment)
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
         options.BaseUrlOverride = server.BaseUrl;
      });

      return builder.Build();
   }
}
