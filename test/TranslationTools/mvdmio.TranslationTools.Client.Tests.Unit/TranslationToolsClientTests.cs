using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public abstract class TranslationToolsClientTests : IDisposable
{
   private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
   protected readonly RecordingHandler Handler;
   protected readonly HttpClient HttpClient;

   protected TranslationToolsClientTests()
   {
      Handler = new RecordingHandler(_responses);
      HttpClient = new HttpClient(Handler);
   }

   public void Dispose()
   {
      TranslationManifestRuntime.UnregisterClient(typeof(TranslationToolsClientTests).Assembly);
      HttpClient.Dispose();
   }

   private static class TestManifestAssemblyMarker
   {
      public static class Keys
      {
      }
   }

   public class Initialize : TranslationToolsClientTests
   {
      [Fact]
      public async Task ShouldHydrateLocaleAndSingleItemCache()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[{"key":"home.title","value":"Hello"}]""");
         using var client = CreateClient([new CultureInfo("en")]);

         await client.Initialize(cancellationToken);
         var translation = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);

         Handler.Requests.Should().HaveCount(1);
         translation.Value.Should().Be("Hello");
      }

      [Fact]
      public async Task ShouldInitializeAllConfiguredSupportedLocales()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[]""");
         EnqueueJson("""[]""");
         using var client = CreateClient([new CultureInfo("nl-NL"), new CultureInfo("en")]);

         await client.Initialize(cancellationToken);

         Handler.Requests.Should().HaveCount(2);
         Handler.Requests.Select(x => x.RequestUri!.PathAndQuery).Should().BeEquivalentTo(["/api/v1/translations/nl-NL", "/api/v1/translations/en"]);
      }
   }

   public class GetAsync : TranslationToolsClientTests
   {
      [Fact]
      public async Task ShouldUseCurrentUICultureForDefaultOverload()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":"Hallo"}""");
         using var client = CreateClient();
         var originalCulture = CultureInfo.CurrentUICulture;

         try
         {
            CultureInfo.CurrentUICulture = new CultureInfo("nl-NL");

            var result = await client.GetAsync("home.title", cancellationToken);

            result.Value.Should().Be("Hallo");
            Handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v1/translations/nl-NL/home.title");
         }
         finally
         {
            CultureInfo.CurrentUICulture = originalCulture;
         }
      }

      [Fact]
      public async Task ShouldSendAuthorizationHeader_AndPreserveLocale()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":"Hello"}""");
         using var client = CreateClient();

         var result = await client.GetAsync("home.title", new CultureInfo("en-US"), cancellationToken);

         result.Value.Should().Be("Hello");
         Handler.Requests.Should().ContainSingle();
         Handler.Requests[0].Headers.GetValues("Authorization").Should().ContainSingle("test-api-key");
         Handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v1/translations/en-US/home.title");
      }

      [Fact]
      public async Task ShouldRejectInvalidKey()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         using var client = CreateClient();

         var action = async () => await client.GetAsync("home title", new CultureInfo("en"), cancellationToken);

         await action.Should().ThrowAsync<ArgumentException>();
         Handler.Requests.Should().BeEmpty();
      }

      [Fact]
      public async Task ShouldUseSingleItemCache_WhenInitialized()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[{"key":"home.title","value":"Hello"}]""");
         using var client = CreateClient();

         await client.Initialize(cancellationToken);
         var result = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);

         result.Value.Should().Be("Hello");
         Handler.Requests.Should().HaveCount(1);
      }

      [Fact]
      public async Task ShouldReuseCachedSingleItem_OnSecondRequest()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":"Hello"}""");
         using var client = CreateClient();

         await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);
         var second = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);

         second.Value.Should().Be("Hello");
         Handler.Requests.Should().ContainSingle();
      }

      [Fact]
      public async Task ShouldStoreFetchedPayloadInCache()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":"Hello"}""");
         using var client = CreateClient();

         var fetched = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);
         var cached = await GetCachedAsync<TranslationItemResponse>(client, "translationtools:item:en:home.title", cancellationToken);

         fetched.Value.Should().Be("Hello");
         cached.Should().NotBeNull();
         cached!.Value.Value.Should().Be("Hello");
      }
   }

   public class GetLocaleAsync : TranslationToolsClientTests
   {
      [Fact]
      public async Task ShouldFetchWholeLocaleDictionaryAndHydrateItemCache()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[{"key":"home.title","value":"Hello"},{"key":"home.subtitle","value":"Welcome"}]""");
         ITranslationToolsClient client = CreateClient();

         var result = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);

         result.Should().BeEquivalentTo(
            new Dictionary<string, string?> {
               ["home.title"] = "Hello",
               ["home.subtitle"] = "Welcome"
            }
         );
         Handler.Requests.Should().ContainSingle();
         Handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v1/translations/en");
         ((TranslationToolsClient)client).TryGetCached("home.title", new CultureInfo("en"))!.Value.Should().Be("Hello");
      }

      [Fact]
      public async Task ShouldReuseCachedLocaleDictionaryOnSubsequentRequests()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[{"key":"home.title","value":"Hello"}]""");
         using var client = CreateClient();

         var first = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);
         var second = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);

          second.Should().BeEquivalentTo(first);
          Handler.Requests.Should().ContainSingle();
       }

       [Fact]
       public async Task RefreshLocaleAsync_ShouldReplaceCachedLocaleValues()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""[{"key":"home.title","value":"Hello"},{"key":"home.subtitle","value":"Welcome"}]""");
          EnqueueJson("""[{"key":"home.title","value":"Hi"}]""");
          using var client = CreateClient();

          await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);
          await client.RefreshLocaleAsync(new CultureInfo("en"), cancellationToken);

          client.TryGetCached("home.title", new CultureInfo("en"))!.Value.Should().Be("Hi");
          client.TryGetCached("home.subtitle", new CultureInfo("en")).Should().BeNull();

          var locale = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);
          locale.Should().BeEquivalentTo(
             new Dictionary<string, string?> {
                ["home.title"] = "Hi"
             }
          );
          Handler.Requests.Should().HaveCount(2);
       }

       [Fact]
       public async Task InvalidateLocale_ShouldRefetchOnNextRead()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""[{"key":"home.title","value":"Hello"}]""");
          EnqueueJson("""[{"key":"home.title","value":"Hi again"}]""");
          using var client = CreateClient();

          var first = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);
          client.InvalidateLocale(new CultureInfo("en"));
          var second = await client.GetLocaleAsync(new CultureInfo("en"), cancellationToken);

          first["home.title"].Should().Be("Hello");
          second["home.title"].Should().Be("Hi again");
          Handler.Requests.Should().HaveCount(2);
       }
    }

    public class CacheUpdates : TranslationToolsClientTests
    {
       [Fact]
       public async Task ApplyLocaleUpdateAsync_ShouldUpdateTryGetCached()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();

          await client.ApplyLocaleUpdateAsync(
             new CultureInfo("en"),
             new Dictionary<string, string?> {
                ["home.title"] = "Hello",
                ["home.subtitle"] = "Welcome"
             },
             cancellationToken
          );

          client.TryGetCached("home.title", new CultureInfo("en"))!.Value.Should().Be("Hello");
          client.TryGetCached("home.subtitle", new CultureInfo("en"))!.Value.Should().Be("Welcome");
       }

       [Fact]
       public async Task ApplyUpdateAsync_ShouldUpdateTryGetCached()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();

          await client.ApplyUpdateAsync(
             new TranslationItemResponse {
                Key = "home.title",
                Value = "Live value"
             },
             new CultureInfo("en"),
             cancellationToken
          );

          client.TryGetCached("home.title", new CultureInfo("en"))!.Value.Should().Be("Live value");
       }

       [Fact]
       public async Task Invalidate_ShouldRemoveCachedItemAndRefetchOnNextRead()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""{"key":"home.title","value":"Hello"}""");
          EnqueueJson("""{"key":"home.title","value":"Hi again"}""");
          using var client = CreateClient();

          var first = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);
          client.Invalidate("home.title", new CultureInfo("en"));
          var second = await client.GetAsync("home.title", new CultureInfo("en"), cancellationToken);

          first.Value.Should().Be("Hello");
          second.Value.Should().Be("Hi again");
          Handler.Requests.Should().HaveCount(2);
       }
    }

     public class LiveUpdateMessages : TranslationToolsClientTests
     {
        [Fact]
        public async Task Processor_ShouldApplyTranslationUpdatedMessage()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();

          await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
             client,
             """{"type":"translation-updated","locale":"en","key":"home.title","value":"Live title"}""",
             cancellationToken
          );

          client.TryGetCached("home.title", new CultureInfo("en"))!.Value.Should().Be("Live title");
       }

       [Fact]
       public async Task Processor_ShouldIgnoreConnectedMessage()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();

          await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
             client,
             """{"type":"connected"}""",
             cancellationToken
          );

          client.TryGetCached("home.title", new CultureInfo("en")).Should().BeNull();
       }

       [Fact]
       public async Task Processor_ShouldIgnoreInvalidPayload()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();

           await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(client, "not-json", cancellationToken);

           client.TryGetCached("home.title", new CultureInfo("en")).Should().BeNull();
      }

      [Fact]
      public async Task Processor_ShouldLogAppliedTranslationUpdatedMessage()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         using var client = CreateClient();
         var logger = new TestLogger<TranslationToolsLiveUpdateService>();

         await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(
            client,
            """{"type":"translation-updated","locale":"en","key":"home.title","value":"Live title"}""",
            logger,
            cancellationToken
         );

         logger.Entries.Should().Contain(x => x.Level == LogLevel.Debug && x.Message.Contains("Applying TranslationTools live update for en home.title", StringComparison.Ordinal));
         logger.Entries.Should().Contain(x => x.Level == LogLevel.Debug && x.Message.Contains("Applied TranslationTools live update for en home.title", StringComparison.Ordinal));
      }

      [Fact]
      public async Task Processor_ShouldLogInvalidPayloadWarning()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         using var client = CreateClient();
         var logger = new TestLogger<TranslationToolsLiveUpdateService>();

         await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(client, "not-json", logger, cancellationToken);

         logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning && x.Message.Contains("Ignoring invalid TranslationTools live update payload.", StringComparison.Ordinal));
      }
   }

   public class LiveUpdateService : TranslationToolsClientTests
   {
      [Fact]
      public async Task StartAsync_ShouldLogSocketTokenFetchFailure()
      {
         using var client = CreateClient();
         var logger = new TestLogger<TranslationToolsLiveUpdateService>();
         using var service = new TranslationToolsLiveUpdateService(
            new StaticHttpClientFactory(new StaticResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))),
            client,
            Options.Create(
               new TranslationToolsClientOptions {
                  ApiKey = "test-api-key",
                  EnableLiveUpdates = true,
                  SupportedLocales = [new CultureInfo("en")]
               }
            ),
            logger
         );

         using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
         await service.StartAsync(cancellationTokenSource.Token);
         await WaitForAsync(
            () => logger.Entries.Any(x => x.Level == LogLevel.Warning && x.Message.Contains("Failed to fetch TranslationTools live update socket token", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken
         );

         logger.Entries.Should().Contain(x => x.Level == LogLevel.Information && x.Message.Contains("Starting TranslationTools live updates.", StringComparison.Ordinal));
         logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning && x.Message.Contains("Failed to fetch TranslationTools live update socket token", StringComparison.Ordinal));

         cancellationTokenSource.Cancel();
      }
   }

     public class ManifestRuntime : TranslationToolsClientTests
     {
       [Fact]
       public async Task ShouldUseRegisteredClientForAsyncReads()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""{"key":"home.title","value":"Hello"}""");
          using var client = CreateClient();
          var originalCulture = CultureInfo.CurrentUICulture;

          try
          {
             CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

             TranslationManifestRuntime.RegisterClient(typeof(TranslationToolsClientTests).Assembly, client);
             var value = await TranslationManifestRuntime.GetAsync(typeof(TranslationToolsClientTests), "checkout.title", cancellationToken: cancellationToken);

             value.Should().Be("Hello");
          }
          finally
          {
             CultureInfo.CurrentUICulture = originalCulture;
          }
      }

      [Fact]
      public async Task ShouldReturnDefaultValue_WhenTranslationValueNull()
      {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""{"key":"checkout.title","value":null}""");
          using var client = CreateClient();
          var originalCulture = CultureInfo.CurrentUICulture;

          try
          {
             CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

             TranslationManifestRuntime.RegisterClient(typeof(TranslationToolsClientTests).Assembly, client);
             var value = await TranslationManifestRuntime.GetAsync(typeof(TranslationToolsClientTests), "checkout.title", defaultValue: "Fallback", cancellationToken: cancellationToken);

             value.Should().Be("Fallback");
          }
          finally
          {
             CultureInfo.CurrentUICulture = originalCulture;
          }
       }

       [Fact]
       public async Task ShouldUseRegisteredDefaultLocale_WhenCurrentCultureIsInvariant()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          EnqueueJson("""{"key":"checkout.title","value":"Hallo"}""");
          using var client = CreateClient();
          var originalCulture = CultureInfo.CurrentUICulture;

          try
          {
             CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

             TranslationManifestRuntime.RegisterDefaultLocale(typeof(TranslationToolsClientTests).Assembly, "nl");
             TranslationManifestRuntime.RegisterClient(typeof(TranslationToolsClientTests).Assembly, client);
             var value = await TranslationManifestRuntime.GetAsync(typeof(TranslationToolsClientTests), "checkout.title", cancellationToken: cancellationToken);

             value.Should().Be("Hallo");
             Handler.Requests.Single().RequestUri!.ToString().Should().Contain("/nl/");
          }
          finally
          {
             CultureInfo.CurrentUICulture = originalCulture;
          }
       }

       [Fact]
       public void ShouldReturnFallbackWithoutFetching_WhenSyncGetMissesSnapshot()
      {
          var originalCulture = CultureInfo.CurrentUICulture;

          try
          {
             CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

             var value = TranslationManifestRuntime.Get(typeof(TranslationToolsClientTests), "checkout.title", "Checkout");

             value.Should().Be("Checkout");
             Handler.Requests.Should().BeEmpty();
          }
          finally
          {
             CultureInfo.CurrentUICulture = originalCulture;
          }
      }

      [Fact]
       public void ShouldReadEmbeddedSnapshot_ForSyncReads()
       {
         var originalCulture = CultureInfo.CurrentUICulture;

         try
         {
            CultureInfo.CurrentUICulture = new CultureInfo("en");

            var value = TranslationManifestRuntime.Get(typeof(TranslationToolsClientTests), "home.title", "Fallback");

            value.Should().Be("Embedded home");
            Handler.Requests.Should().BeEmpty();
         }
         finally
         {
            CultureInfo.CurrentUICulture = originalCulture;
           }
        }

       [Fact]
       public async Task ShouldPreferRegisteredClientCache_ForSyncReads()
       {
          var cancellationToken = TestContext.Current.CancellationToken;
          using var client = CreateClient();
          var originalCulture = CultureInfo.CurrentUICulture;

          try
          {
             CultureInfo.CurrentUICulture = new CultureInfo("en");
             await client.ApplyUpdateAsync(
                new TranslationItemResponse {
                   Key = "home.title",
                   Value = "Live home"
                },
                new CultureInfo("en"),
                cancellationToken
             );

             TranslationManifestRuntime.RegisterClient(typeof(TranslationToolsClientTests).Assembly, client);
             var value = TranslationManifestRuntime.Get(typeof(TranslationToolsClientTests), "home.title", "Fallback");

             value.Should().Be("Live home");
             Handler.Requests.Should().BeEmpty();
          }
          finally
          {
             CultureInfo.CurrentUICulture = originalCulture;
          }
       }
    }

    public class DependencyInjection : TranslationToolsClientTests
    {
      [Fact]
       public void AddTranslationToolsClient_ShouldPickUpRequestLocalizationSupportedLocales()
       {
         var services = new ServiceCollection();

         services.Configure<RequestLocalizationOptions>(options => {
               options.SetDefaultCulture("en").AddSupportedCultures("en", "nl").AddSupportedUICultures("en", "nl");
            }
         );

         services.AddTranslationToolsClient(options => {
               options.ApiKey = "test-api-key";
            }
         );

         using var serviceProvider = services.BuildServiceProvider();
         var options = serviceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;

          options.SupportedLocales.Select(x => x.Name).Should().Equal(["en", "nl"]);
       }

       [Fact]
       public void GetManifestAssemblies_ShouldIncludeLoadedManifestAssemblies()
       {
          var assemblies = DependencyInjectionExtensions.GetManifestAssemblies();

          assemblies.Should().Contain(typeof(TestManifestAssemblyMarker).Assembly);
       }
    }

   protected TranslationToolsClient CreateClient(CultureInfo[]? supportedLocales = null)
   {
      return new TranslationToolsClient(HttpClient, CreateOptions(supportedLocales), new LocalTranslationToolsClientCache());
   }

   protected void EnqueueJson(string body)
   {
      _responses.Enqueue(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
               Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return response;
         }
      );
   }

   private static IOptions<TranslationToolsClientOptions> CreateOptions(CultureInfo[]? supportedLocales)
   {
      return Options.Create(
         new TranslationToolsClientOptions {
            ApiKey = "test-api-key",
            SupportedLocales = supportedLocales ?? [new CultureInfo("en")]
         }
      );
   }

    private static async Task<TranslationToolsClientCacheEntry<T>?> GetCachedAsync<T>(TranslationToolsClient client, string key, CancellationToken cancellationToken)
       where T : class
    {
       var cacheField = typeof(TranslationToolsClient).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
       var cache = (ITranslationToolsClientCache)cacheField.GetValue(client)!;
       return await cache.GetAsync<T>(key, cancellationToken);
    }

     private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
     {
       var startedAt = DateTimeOffset.UtcNow;

       while (DateTimeOffset.UtcNow - startedAt < timeout)
       {
          cancellationToken.ThrowIfCancellationRequested();

          if (predicate())
             return;

          await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
       }

       throw new TimeoutException("Condition was not met within the allotted time.");
     }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
      private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;
      public List<HttpRequestMessage> Requests { get; } = [];

      public RecordingHandler(Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses)
      {
         _responses = responses;
      }

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         Requests.Add(Clone(request));

         if (_responses.Count == 0)
            throw new InvalidOperationException("No queued response.");

         return Task.FromResult(_responses.Dequeue()(request));
      }

      private static HttpRequestMessage Clone(HttpRequestMessage request)
      {
         var clone = new HttpRequestMessage(request.Method, request.RequestUri);

         foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

          return clone;
       }
    }

    protected sealed class StaticHttpClientFactory : IHttpClientFactory
    {
       private readonly HttpMessageHandler _handler;

       public StaticHttpClientFactory(HttpMessageHandler handler)
       {
          _handler = handler;
       }

       public HttpClient CreateClient(string name)
       {
          return new HttpClient(_handler, disposeHandler: false);
       }
    }

    protected sealed class StaticResponseHandler : HttpMessageHandler
    {
       private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

       public StaticResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
       {
          _responseFactory = responseFactory;
       }

       protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
       {
          return Task.FromResult(_responseFactory(request));
       }
    }

    protected sealed class TestLogger<T> : ILogger<T>
    {
       private readonly object _syncRoot = new();
       public List<LogEntry> Entries { get; } = [];

       public IDisposable BeginScope<TState>(TState state)
          where TState : notnull
       {
          return NullScope.Instance;
       }

       public bool IsEnabled(LogLevel logLevel)
       {
          return true;
       }

       public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
       {
          lock (_syncRoot)
             Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
       }
    }

    protected sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
       public static NullScope Instance { get; } = new();

       public void Dispose()
       {
       }
    }
}
