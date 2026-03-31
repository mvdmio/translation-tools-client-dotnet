using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using mvdmio.TranslationTools.Client.Internal;

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
}
