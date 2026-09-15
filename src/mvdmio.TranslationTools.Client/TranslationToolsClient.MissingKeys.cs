using Microsoft.Extensions.Logging;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

public sealed partial class TranslationToolsClient
{
   private async Task TrySendMissingKeysAsync(CancellationToken cancellationToken)
   {
      if (_catalog.Count == 0)
         return;

      // No supported-locale GET ran, so we have no snapshot of keys the service already has.
      if (GetSupportedLocales().Length == 0)
         return;

      try
      {
         var knownKeys = _cache.GetKnownKeys();
         var missing = _catalog
            .Where(entry => !knownKeys.Contains(entry.Translation))
            .ToArray();

         if (missing.Length == 0)
            return;

         var items = BuildMissingKeyItems(missing, Options.DefaultLocale);
         if (items.Length == 0)
            return;

         await PushProjectItemsAsync(items, cancellationToken);
         await MergeSentItemsIntoCacheAsync(items, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         _logger?.LogWarning(exception, "TranslationTools missing-key send failed.");
      }
   }

   private static ProjectPushItemRequest[] BuildMissingKeyItems(IReadOnlyList<TranslationCatalogKey> missing, string defaultLocale)
   {
      var items = new List<ProjectPushItemRequest>();

      foreach (var entry in missing)
      {
         var valuesByLocale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         if (!string.IsNullOrEmpty(entry.NeutralValue))
            valuesByLocale[defaultLocale] = entry.NeutralValue;

         foreach (var pair in entry.LocaleValues)
         {
            if (string.IsNullOrEmpty(pair.Value))
               continue;

            valuesByLocale[pair.Key] = pair.Value;
         }

         foreach (var pair in valuesByLocale.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
         {
            items.Add(
               new ProjectPushItemRequest
               {
                  Origin = entry.Origin,
                  Locale = pair.Key,
                  Key = entry.Key,
                  Value = pair.Value
               }
            );
         }
      }

      return items.ToArray();
   }

   private async Task PushProjectItemsAsync(ProjectPushItemRequest[] items, CancellationToken cancellationToken)
   {
      var payload = new ProjectPushRequest
      {
         Items = items,
         Environment = _environment,
         Prune = false
      };

      await PostProjectPushAsync(payload, cancellationToken);
   }

   private async Task MergeSentItemsIntoCacheAsync(ProjectPushItemRequest[] items, CancellationToken cancellationToken)
   {
      foreach (var item in items)
      {
         var locale = EffectiveLocale.Resolve(item.Locale, Options.DefaultLocale);

         await StoreTranslationAsync(
            locale,
            new TranslationItemResponse
            {
               Origin = item.Origin,
               Key = item.Key,
               Value = item.Value
            },
            cancellationToken
         );
      }
   }
}
