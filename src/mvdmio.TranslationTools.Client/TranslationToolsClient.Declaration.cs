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
   private async Task TrySendDeclarationAsync(CancellationToken cancellationToken)
   {
      try
      {
         var catalog = _catalogSource();
         if (catalog.Count == 0)
            return;

         var payload = new DeclarationRequest
         {
            Items = BuildDeclarationItems(catalog, Options.DefaultLocale),
            Environment = _environment
         };

         await PostJsonAsync("api/v1/translations/declaration", payload, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         _logger?.LogWarning(exception, "TranslationTools declaration failed.");
      }
   }

   private static KeyValueItemRequest[] BuildDeclarationItems(IReadOnlyList<TranslationCatalogKey> catalog, string defaultLocale)
   {
      var items = new List<KeyValueItemRequest>();

      foreach (var entry in catalog)
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

         // A key with no value anywhere is still declared, so the service keeps it.
         if (valuesByLocale.Count == 0)
            valuesByLocale[defaultLocale] = string.Empty;

         foreach (var pair in valuesByLocale.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
         {
            items.Add(
               new KeyValueItemRequest
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
}
