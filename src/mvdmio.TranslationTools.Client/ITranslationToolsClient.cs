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
   TranslationItemResponse Get(TranslationRef translation) => AsyncHelper.RunSync(() => GetAsync(translation));

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   TranslationItemResponse Get(TranslationRef translation, CultureInfo locale) => AsyncHelper.RunSync(() => GetAsync(translation, locale));

   /// <summary>
   /// Get a translation for a specific locale and seed missing per-locale values on the server.
   /// </summary>
   TranslationItemResponse Get(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues) => AsyncHelper.RunSync(() => GetAsync(translation, locale, defaultValue, localeValues));

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default);

   /// <summary>
   /// Get a translation for a specific locale and seed missing per-locale values on the server.
   /// </summary>
   /// <remarks>
   /// Default implementation ignores <paramref name="defaultValue"/> and <paramref name="localeValues"/> and delegates to <see cref="GetAsync(TranslationRef, CultureInfo, CancellationToken)"/>. Implementations should override to support server-side seeding.
   /// </remarks>
   Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
      => GetAsync(translation, locale, cancellationToken);

   /// <summary>
   /// Get all translations for a specific locale.
   /// </summary>
   Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default);
}
