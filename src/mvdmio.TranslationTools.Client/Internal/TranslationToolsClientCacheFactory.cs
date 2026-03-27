using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
#if NET10_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
#endif

namespace mvdmio.TranslationTools.Client.Internal;

internal static class TranslationToolsClientCacheFactory
{
   public static ITranslationToolsClientCache Create(IServiceProvider serviceProvider)
   {
      var options = serviceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;

#if NET10_0_OR_GREATER
      var hybridCache = serviceProvider.GetService<HybridCache>();
      if (hybridCache is not null)
         return new HybridTranslationToolsClientCache(hybridCache, options.CacheDuration);
#endif

      var distributedCache = serviceProvider.GetService<IDistributedCache>();
      if (distributedCache is not null)
         return new DistributedTranslationToolsClientCache(distributedCache, options.CacheDuration);

      var memoryCache = serviceProvider.GetService<IMemoryCache>();
      if (memoryCache is not null)
         return new MemoryTranslationToolsClientCache(memoryCache, options.CacheDuration);

      return new LocalTranslationToolsClientCache(options.CacheDuration);
   }
}
