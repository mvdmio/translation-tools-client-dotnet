using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
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
      HttpClient.Dispose();
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
      public async Task TranslateGetAsync_ShouldSendEncodedDefaultValue()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":null}""");
         using var client = CreateClient();

         Translate.Configure(client);
         _ = await Translate.GetAsync("home.title", new CultureInfo("en"), "Hello world & more", cancellationToken);

         Handler.Requests.Should().ContainSingle();
         Handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v1/translations/en/home.title?defaultValue=Hello%20world%20%26%20more");
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
   }

   public class TranslateStatic : TranslationToolsClientTests
   {
      [Fact]
      public async Task ShouldUseConfiguredClient()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":"Hello"}""");
         using var client = CreateClient();

         Translate.Configure(client);
         var value = await Translate.GetAsync("home.title", cancellationToken: cancellationToken);

         value.Should().Be("Hello");
      }

      [Fact]
      public async Task ShouldReturnDefaultValue_WhenTranslationValueNull()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""{"key":"home.title","value":null}""");
         using var client = CreateClient();

         Translate.Configure(client);
         var value = await Translate.GetAsync("home.title", "Fallback", cancellationToken);

         value.Should().Be("Fallback");
      }

      [Fact]
      public async Task ShouldUseCurrentUICultureForSyncGet()
      {
         var cancellationToken = TestContext.Current.CancellationToken;
         EnqueueJson("""[{"key":"home.title","value":"Hallo"}]""");
         using var client = CreateClient([new CultureInfo("nl-NL")]);
         var originalCulture = CultureInfo.CurrentUICulture;

         try
         {
            Translate.Configure(client);
            await client.Initialize(cancellationToken);
            CultureInfo.CurrentUICulture = new CultureInfo("nl-NL");

            var value = Translate.Get("home.title");

            value.Should().Be("Hallo");
            Handler.Requests.Should().ContainSingle();
         }
         finally
         {
            CultureInfo.CurrentUICulture = originalCulture;
         }
      }

      [Fact]
      public void ShouldReturnFallbackWithoutFetching_WhenSyncGetMissesCache()
      {
         using var client = CreateClient();

         Translate.Configure(client);
         var value = Translate.Get("checkout.title", "Checkout");

         value.Should().Be("Checkout");
         Handler.Requests.Should().BeEmpty();
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
      public void ShouldUseMemoryCache_WhenRegistered()
      {
         var serviceProvider = CreateServiceProvider(services => services.AddMemoryCache());

         var cache = TranslationToolsClientCacheFactory.Create(serviceProvider);

         cache.Should().BeOfType<MemoryTranslationToolsClientCache>();
      }

      [Fact]
      public void ShouldUseDistributedCache_WhenRegistered()
      {
         var serviceProvider = CreateServiceProvider(services => services.AddSingleton<IDistributedCache, FakeDistributedCache>());

         var cache = TranslationToolsClientCacheFactory.Create(serviceProvider);

         cache.Should().BeOfType<DistributedTranslationToolsClientCache>();
      }

      [Fact]
      public void ShouldUseHybridCache_WhenRegistered()
      {
         var serviceProvider = CreateServiceProvider(services => {
               services.AddSingleton<IDistributedCache, FakeDistributedCache>();
               services.AddSingleton<HybridCache, FakeHybridCache>();
            }
         );

         var cache = TranslationToolsClientCacheFactory.Create(serviceProvider);

         cache.Should().BeOfType<HybridTranslationToolsClientCache>();
      }
   }

   protected TranslationToolsClient CreateClient(CultureInfo[]? supportedLocales = null)
   {
      return new TranslationToolsClient(HttpClient, CreateOptions(supportedLocales), new LocalTranslationToolsClientCache(TimeSpan.FromMinutes(5)));
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
            SupportedLocales = supportedLocales ?? [new CultureInfo("en")],
            CacheDuration = TimeSpan.FromMinutes(5)
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

   private static ServiceProvider CreateServiceProvider(Action<IServiceCollection> configureServices)
   {
      var services = new ServiceCollection();

      services.Configure<TranslationToolsClientOptions>(options => {
            options.ApiKey = "test-api-key";
            options.CacheDuration = TimeSpan.FromMinutes(5);
         }
      );

      configureServices(services);
      return services.BuildServiceProvider();
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

   private sealed class FakeDistributedCache : IDistributedCache
   {
      private readonly ConcurrentDictionary<string, byte[]> _entries = new();

      public byte[]? Get(string key)
      {
         _entries.TryGetValue(key, out var value);
         return value;
      }

      public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
      {
         return Task.FromResult(Get(key));
      }

      public void Refresh(string key)
      {
      }

      public Task RefreshAsync(string key, CancellationToken token = default)
      {
         return Task.CompletedTask;
      }

      public void Remove(string key)
      {
         _entries.TryRemove(key, out _);
      }

      public Task RemoveAsync(string key, CancellationToken token = default)
      {
         Remove(key);
         return Task.CompletedTask;
      }

      public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
      {
         _entries[key] = value;
      }

      public Task SetAsync(
         string key,
         byte[] value,
         DistributedCacheEntryOptions options,
         CancellationToken token = default
      )
      {
         Set(key, value, options);
         return Task.CompletedTask;
      }
   }

   private sealed class FakeHybridCache : HybridCache
   {
      private readonly ConcurrentDictionary<string, object?> _entries = new();

      public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
      {
         _entries.TryRemove(key, out _);
         return ValueTask.CompletedTask;
      }

      public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
      {
         return ValueTask.CompletedTask;
      }

      public override ValueTask<T> GetOrCreateAsync<TState, T>(
         string key,
         TState state,
         Func<TState, CancellationToken, ValueTask<T>> underlyingDataCallback,
         HybridCacheEntryOptions? options = null,
         IEnumerable<string>? tags = null,
         CancellationToken cancellationToken = default
      )
      {
         if (_entries.TryGetValue(key, out var value) && value is T typed)
            return ValueTask.FromResult(typed);

         return underlyingDataCallback(state, cancellationToken);
      }

      public override ValueTask SetAsync<T>(
         string key,
         T value,
         HybridCacheEntryOptions? options = null,
         IEnumerable<string>? tags = null,
         CancellationToken cancellationToken = default
      )
      {
         _entries[key] = value;
         return ValueTask.CompletedTask;
      }
   }
}
