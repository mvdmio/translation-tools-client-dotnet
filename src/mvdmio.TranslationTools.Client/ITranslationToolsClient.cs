using JetBrains.Annotations;
using mvdmio.TranslationTools.Client.Internal;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Translation API client.
/// </summary>
[PublicAPI]
public interface ITranslationToolsClient
{
   private static TranslationRef Default(string key) => new("/Localizations.resx", key);

   /// <summary>
   /// Preload the default locale into the internal cache.
   /// </summary>
   Task Initialize(CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   TranslationItemResponse Get(TranslationRef translation) => AsyncHelper.RunSync(() => GetAsync(translation));
   TranslationItemResponse Get(string key) => Get(Default(key));
   
   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   TranslationItemResponse Get(TranslationRef translation, CultureInfo locale) => AsyncHelper.RunSync(() => GetAsync(translation, locale));
   TranslationItemResponse Get(string key, CultureInfo locale) => Get(Default(key), locale);

   /// <summary>
   /// Try to get a translation from the local cache using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   TranslationItemResponse? TryGetCached(TranslationRef translation);
   TranslationItemResponse? TryGetCached(string key) => TryGetCached(Default(key));

   /// <summary>
   /// Try to get a translation from the local cache for a specific locale.
   /// </summary>
   TranslationItemResponse? TryGetCached(TranslationRef translation, CultureInfo locale);
   TranslationItemResponse? TryGetCached(string key, CultureInfo locale) => TryGetCached(Default(key), locale);

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default);
   Task<TranslationItemResponse> GetAsync(string key, CancellationToken cancellationToken = default) => GetAsync(Default(key), cancellationToken);

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default);
   Task<TranslationItemResponse> GetAsync(string key, CultureInfo locale, CancellationToken cancellationToken = default) => GetAsync(Default(key), locale, cancellationToken);

   /// <summary>
   /// Get all translations for a specific locale.
   /// </summary>
   Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default);

   /// <summary>
   /// Refresh the cached locale payload from the server.
   /// </summary>
   Task RefreshLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default);

   /// <summary>
   /// Remove all cached translations for a locale.
   /// </summary>
   void InvalidateLocale(CultureInfo locale);

   /// <summary>
   /// Remove one cached translation.
   /// </summary>
   void Invalidate(TranslationRef translation, CultureInfo locale);
   void Invalidate(string key, CultureInfo locale) => Invalidate(Default(key), locale);

   /// <summary>
   /// Replace the cached locale payload with externally supplied values.
   /// </summary>
   Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<TranslationRef, string?> values, CancellationToken cancellationToken = default);
   Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);

   /// <summary>
   /// Apply one externally supplied translation update to the cache.
   /// </summary>
   Task ApplyUpdateAsync(TranslationRef translation, string? value, CultureInfo locale, CancellationToken cancellationToken = default);
   Task ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default)
   {
      return ApplyUpdateAsync(new TranslationRef(item.Origin, item.Key), item.Value, locale, cancellationToken);
   }
}
