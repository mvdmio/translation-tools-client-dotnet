using JetBrains.Annotations;
using mvdmio.TranslationTools.Client.Internal;
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
   TranslationItemResponse Get(TranslationRef translation) => AsyncHelper.RunSync(() => GetAsync(translation));

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   TranslationItemResponse Get(TranslationRef translation, CultureInfo locale) => AsyncHelper.RunSync(() => GetAsync(translation, locale));

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get all translations for a specific locale.
   /// </summary>
   Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default);
}
