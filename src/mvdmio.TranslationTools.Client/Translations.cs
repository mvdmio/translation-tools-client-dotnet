using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Static access helpers used by generated localization classes.
/// </summary>
public static class Translations
{
   private static ITranslationToolsClient? _client;

   internal static void SetClient(ITranslationToolsClient client)
   {
      _client = client;
   }

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public static string Get(TranslationRef translation, string? defaultValue = null)
   {
      return Get(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues: null);
   }

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/> and seed the server with per-locale values when missing.
   /// </summary>
   public static string Get(TranslationRef translation, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues)
   {
      return Get(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues);
   }

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   public static string Get(TranslationRef translation, CultureInfo locale, string? defaultValue = null)
   {
      return Get(translation, locale, defaultValue, localeValues: null);
   }

   /// <summary>
   /// Get a translation for a specific locale and seed the server with per-locale values when missing.
   /// </summary>
   public static string Get(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues)
   {
      var client = ResolveClient();
      var item = client.Get(translation, locale, defaultValue, localeValues);
      return item.Value ?? defaultValue ?? translation.Key;
   }

   /// <summary>
   /// Get a translation asynchronously using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public static Task<string> GetAsync(TranslationRef translation, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues: null, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously using <see cref="CultureInfo.CurrentUICulture"/> and seed the server with per-locale values when missing.
   /// </summary>
   public static Task<string> GetAsync(TranslationRef translation, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale.
   /// </summary>
   public static Task<string> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return GetAsync(translation, locale, defaultValue, localeValues: null, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale and seed the server with per-locale values when missing.
   /// </summary>
   public static async Task<string> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, CancellationToken cancellationToken = default)
   {
      var client = ResolveClient();
      var response = await client.GetAsync(translation, locale, defaultValue, localeValues, cancellationToken);
      return response.Value ?? defaultValue ?? translation.Key;
   }

   private static ITranslationToolsClient ResolveClient()
   {
      return _client ?? throw new InvalidOperationException("Translations is not initialized. Call app.InitializeTranslationToolsClientAsync() during startup.");
   }
}
