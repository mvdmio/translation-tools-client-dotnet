using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Internal;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientCacheTests
{
   [Fact]
   public async Task Cache_ShouldStoreRetrieveAndRemoveTypedEntries()
   {
      var cache = new LocalTranslationToolsClientCache();
      var entry = new TranslationToolsClientCacheEntry<TestValue>
      {
         Value = new TestValue { Name = "value" }
      };

      await cache.SetAsync("key", entry, TestContext.Current.CancellationToken);

      cache.Get<TestValue>("key")!.Value.Name.Should().Be("value");
      (await cache.GetAsync<TestValue>("key", TestContext.Current.CancellationToken))!.Value.Name.Should().Be("value");
      cache.Get<string>("key").Should().BeNull();

      await cache.RemoveAsync("key", TestContext.Current.CancellationToken);

      cache.Get<TestValue>("key").Should().BeNull();
   }

   private sealed class TestValue
   {
      public required string Name { get; init; }
   }
}
