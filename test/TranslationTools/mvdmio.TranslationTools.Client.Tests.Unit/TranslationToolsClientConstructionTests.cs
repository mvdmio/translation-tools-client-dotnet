using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using System.Net.Http;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientConstructionTests
{
   [Fact]
   public void Constructor_ShouldThrow_WhenApiKeyIsBlank()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = " "
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*ApiKey*");
   }

   [Fact]
   public void Constructor_ShouldThrow_WhenDefaultLocaleIsBlank()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = " "
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*DefaultLocale*");
   }

   [Fact]
   public void Constructor_ShouldThrow_WhenDefaultLocaleIsNotARecognisedLocale()
   {
      var act = () => new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            DefaultLocale = "!!!"
         }),
         new LocalTranslationToolsClientCache()
      );

      act.Should().Throw<ArgumentException>().WithMessage("*DefaultLocale*");
   }

   [Theory]
   [InlineData("prod:sha")]
   [InlineData("prod env")]
   [InlineData("prod/east")]
   [InlineData("prod!")]
   [InlineData(".")]
   [InlineData("..")]
   [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
   public void Constructor_ShouldThrow_WhenEnvironmentIsIllegal(string environment)
   {
      var act = () => CreateClient(environment: environment);

      var exception = act.Should().Throw<ArgumentException>().Which;
      exception.Message.Should().Contain("letters");
      exception.Message.Should().Contain("64");
   }

   [Theory]
   [InlineData(null)]
   [InlineData("")]
   [InlineData("   ")]
   [InlineData("production")]
   [InlineData("staging")]
   [InlineData("Production")]
   [InlineData("dev_local")]
   [InlineData("build-123")]
   [InlineData("v1.2")]
   [InlineData(" production ")]
   [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
   public void Constructor_ShouldSucceed_WhenEnvironmentIsLegalOrUnnamed(string? environment)
   {
      var act = () => CreateClient(environment: environment, enableHeartbeat: false);

      act.Should().NotThrow();
   }

   [Fact]
   public void Constructor_ShouldPerformNoHttpRequest_WhenEnvironmentIsIllegal()
   {
      var handler = new RecordingHandler();

      var act = () => CreateClient(handler, environment: "prod:sha", enableHeartbeat: false);

      var exception = act.Should().Throw<ArgumentException>().Which;
      exception.Message.Should().Contain("letters");
      exception.Message.Should().Contain("64");
      handler.RequestCount.Should().Be(0);
   }

   [Fact]
   public void Constructor_ShouldPerformNoHttpRequest_WhenEnvironmentIsIllegal_EvenWithHeartbeatEnabled()
   {
      var handler = new RecordingHandler();

      var act = () => CreateClient(handler, environment: "prod:sha", enableHeartbeat: true);

      act.Should().Throw<ArgumentException>();
      handler.RequestCount.Should().Be(0);
   }

   [Fact]
   public async Task Constructor_ShouldSendEnvironmentTrimmedAndUnlowercased_WhenLegal()
   {
      var handler = new RecordingHandler();
      using var client = CreateClient(handler, environment: " Production ", enableHeartbeat: false);

      await client.GetLocaleAsync(new CultureInfo("en"), TestContext.Current.CancellationToken);

      handler.LastGetPath.Should().Be("/api/v1/translations/en/Production");
   }

   private static TranslationToolsClient CreateClient(
      string? environment,
      bool enableHeartbeat = true)
   {
      return CreateClient(new EmptySuccessHandler(), environment, enableHeartbeat);
   }

   private static TranslationToolsClient CreateClient(
      HttpMessageHandler handler,
      string? environment,
      bool enableHeartbeat = true)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            Environment = environment,
            EnableHeartbeat = enableHeartbeat
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

   private sealed class RecordingHandler : HttpMessageHandler
   {
      private int _requestCount;

      public int RequestCount => _requestCount;

      public string? LastGetPath { get; private set; }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         Interlocked.Increment(ref _requestCount);

         if (request.Method == HttpMethod.Get)
            LastGetPath = request.RequestUri?.AbsolutePath;

         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]")
         });
      }
   }
}
