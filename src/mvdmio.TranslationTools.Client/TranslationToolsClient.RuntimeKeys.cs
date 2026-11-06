using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

public sealed partial class TranslationToolsClient
{
   /// <inheritdoc />
   public async Task DeclareAsync(IEnumerable<TranslationCatalogKey> keys, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(keys);

      var catalog = keys.ToArray();
      if (catalog.Length == 0)
         return;

      var payload = new DeclarationRequest
      {
         Items = BuildDeclarationItems(catalog, Options.DefaultLocale),
         Environment = _environment
      };

      await PostJsonAsync("api/v1/translations/declaration", payload, cancellationToken);
   }

   /// <inheritdoc />
   public async Task WriteAsync(IEnumerable<TranslationValueWrite> values, CancellationToken cancellationToken = default)
   {
      ArgumentNullException.ThrowIfNull(values);

      var writes = values.ToArray();
      if (writes.Length == 0)
         return;

      var items = writes.Select(BuildWriteItem).ToArray();

      // No globals: a Write never touches the Environment's Global Placeholders.
      var payload = new ProjectPushRequest
      {
         Items = items,
         Environment = _environment
      };

      await PostJsonAsync("api/v1/translations/project", payload, cancellationToken);

      // The service accepted the Write, so the writer's own lookups answer the new text at once.
      foreach (var write in writes)
         await ApplyUpdateAsync(write.Translation, write.Value, write.Locale, cancellationToken);
   }

   /// <inheritdoc />
   public async Task<string?> GetStoredValueAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      var lookup = new TranslationLookupRequest(translation, ResolveEffectiveLocale(locale), neutralValue: null, localeValues: null);
      using var request = lookup.ToHttpRequest(_environment);

      // Bounded like a lookup, but it neither checks nor opens the suppression window, and it never
      // reads or writes the cache: the text fills an edit form, so it must come from the service.
      using var timeoutCts = new CancellationTokenSource(Options.LookupTimeout, _timeProvider);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

      try
      {
         var response = await FetchAsync<TranslationItemResponse>(request, linkedCts.Token);
         return string.IsNullOrWhiteSpace(response.Value) ? null : response.Value;
      }
      catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
      {
         throw new TranslationLookupException(
            $"Reading the stored value of key '{translation.Key}' in locale '{lookup.Locale.Name}' failed: the service could not be reached within {Options.LookupTimeout}.",
            exception
         );
      }
   }

   private KeyValueItemRequest BuildWriteItem(TranslationValueWrite write)
   {
      ArgumentNullException.ThrowIfNull(write);

      if (write.Value is null)
         throw new ArgumentNullException(nameof(write), $"The value to write for key '{write.Translation.Key}' in origin '{write.Translation.Origin}' is null. Write an empty string to clear the text.");

      ArgumentNullException.ThrowIfNull(write.Locale, nameof(write));

      return new KeyValueItemRequest
      {
         Origin = write.Translation.Origin,
         Locale = ResolveEffectiveLocale(write.Locale).Name,
         Key = write.Translation.Key,
         Value = write.Value
      };
   }
}
