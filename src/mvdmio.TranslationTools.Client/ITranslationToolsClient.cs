using JetBrains.Annotations;
using mvdmio.TranslationTools.Client.Internal;
using System;
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
   /// Send the Declaration (every local catalog key, with its Neutral value as the default locale plus
   /// sibling-locale values) to the service, then preload supported locales into the internal cache.
   /// The service stores a value only where it holds none, so it never overwrites a stored value.
   /// A failed Declaration is logged and does not throw. A locale that fails to preload is logged as a
   /// warning and does not throw; the next locale is preloaded, the heartbeat starts, and the first lookup
   /// that needs the failed locale fetches it. A cancelled <paramref name="cancellationToken"/> still throws.
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

   /// <summary>
   /// Declare keys to the service at any time, for a key under any Origin. The service adds the keys to the
   /// client's Environment and stores a value only where it holds none, so it never overwrites a stored value.
   /// Declaring a key again renews its 7-day expiry. The keys never enter the catalog.
   /// </summary>
   /// <param name="keys">The keys to declare, each with optional values per locale. An empty set sends nothing.</param>
   /// <param name="cancellationToken">Token that cancels the request.</param>
   /// <returns>A task that completes when the service accepted the Declaration.</returns>
   /// <remarks>
   /// Unlike the startup Declaration, a failure throws: an <see cref="System.Net.Http.HttpRequestException"/> for a failing
   /// status or a transport failure, and an <see cref="OperationCanceledException"/> for a cancelled token.
   /// Default implementation throws <see cref="NotSupportedException"/>.
   /// </remarks>
   /// <exception cref="ArgumentNullException"><paramref name="keys"/> is null.</exception>
   Task DeclareAsync(IEnumerable<TranslationCatalogKey> keys, CancellationToken cancellationToken = default)
      => throw new NotSupportedException($"{GetType().Name} does not support {nameof(DeclareAsync)}.");

   /// <summary>
   /// Write values to the service at any time, for a key under any Origin. The service overwrites each value that
   /// differs, keeps the previous text in the key's history, opens the Review and Auto-translation work a Push opens,
   /// and declares a key that is new to the Environment. The Environment's Global Placeholders are untouched.
   /// After the service accepts the Write, this client's cache answers the written values at once.
   /// </summary>
   /// <param name="values">The values to write. An empty string clears the text. An empty set sends nothing.</param>
   /// <param name="cancellationToken">Token that cancels the request.</param>
   /// <returns>A task that completes when the service accepted the Write.</returns>
   /// <remarks>
   /// A failure throws, and the cache stays as it was.
   /// Default implementation throws <see cref="NotSupportedException"/>.
   /// </remarks>
   /// <exception cref="ArgumentNullException"><paramref name="values"/> is null, or an entry has a null value or locale. Nothing is sent.</exception>
   Task WriteAsync(IEnumerable<TranslationValueWrite> values, CancellationToken cancellationToken = default)
      => throw new NotSupportedException($"{GetType().Name} does not support {nameof(WriteAsync)}.");

   /// <summary>
   /// Read the text the service stores for one key and locale. Unlike <see cref="GetAsync(TranslationRef, CultureInfo, CancellationToken)"/>
   /// it always asks the service: it bypasses the cache, the local fallback, and the suppression window, and it never writes the cache.
   /// </summary>
   /// <param name="translation">The key to read, under any Origin.</param>
   /// <param name="locale">The locale to read. A blank locale reads the configured default locale.</param>
   /// <param name="cancellationToken">Token that cancels the request.</param>
   /// <returns>The stored text as the service holds it, or null when the service stores nothing (no row, a null value, or an empty or whitespace value).</returns>
   /// <remarks>
   /// This call fills an edit form, so a failure must not look like "nothing stored": it always throws when the service cannot answer
   /// and does not follow <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/>. A failing status or a transport failure throws an
   /// <see cref="System.Net.Http.HttpRequestException"/>, a body the client cannot read throws, a timeout (<see cref="TranslationToolsClientOptions.LookupTimeout"/>)
   /// throws a <see cref="TranslationLookupException"/>, and a cancelled token throws an <see cref="OperationCanceledException"/>.
   /// Default implementation throws <see cref="NotSupportedException"/>.
   /// </remarks>
   Task<string?> GetStoredValueAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
      => throw new NotSupportedException($"{GetType().Name} does not support {nameof(GetStoredValueAsync)}.");
}
