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
   /// <summary>
   /// Preload the default locale into the internal cache.
   /// </summary>
   Task Initialize(CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   TranslationItemResponse Get(string key) => AsyncHelper.RunSync(() => GetAsync(key));

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   TranslationItemResponse Get(string key, CultureInfo locale) => AsyncHelper.RunSync(() => GetAsync(key, locale));

   /// <summary>
   /// Try to get a translation from the local cache using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   TranslationItemResponse? TryGetCached(string key);

   /// <summary>
   /// Try to get a translation from the local cache for a specific locale.
   /// </summary>
   TranslationItemResponse? TryGetCached(string key, CultureInfo locale);

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(string key, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(string key, CultureInfo locale, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get all translations for a specific locale.
   /// </summary>
   Task<IReadOnlyDictionary<string, string?>> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default);

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
   void Invalidate(string key, CultureInfo locale);

   /// <summary>
   /// Replace the cached locale payload with externally supplied values.
   /// </summary>
   Task ApplyLocaleUpdateAsync(CultureInfo locale, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);

   /// <summary>
   /// Apply one externally supplied translation update to the cache.
   /// </summary>
   Task ApplyUpdateAsync(TranslationItemResponse item, CultureInfo locale, CancellationToken cancellationToken = default);
}
