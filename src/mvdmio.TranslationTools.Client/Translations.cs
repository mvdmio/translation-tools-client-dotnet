using System;
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
      return Get(translation, CultureInfo.CurrentUICulture, defaultValue);
   }

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   public static string Get(TranslationRef translation, CultureInfo locale, string? defaultValue = null)
   {
      var client = ResolveClient();

      if (client is TranslationToolsClient translationToolsClient)
      {
         var cached = translationToolsClient.TryGetCached(translation, locale);
         return cached?.Value ?? defaultValue ?? translation.Key;
      }

      var item = client.Get(translation, locale);
      return item.Value ?? defaultValue ?? translation.Key;
   }

   /// <summary>
   /// Get a translation asynchronously using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public static async Task<string> GetAsync(TranslationRef translation, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return await GetAsync(translation, CultureInfo.CurrentUICulture, defaultValue, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale.
   /// </summary>
   public static async Task<string> GetAsync(TranslationRef translation, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      var client = ResolveClient();

      if (client is TranslationToolsClient translationToolsClient)
      {
         var item = await translationToolsClient.GetAsync(translation, locale, defaultValue, cancellationToken);
         return item.Value ?? defaultValue ?? translation.Key;
      }

      var response = await client.GetAsync(translation, locale, cancellationToken);
      return response.Value ?? defaultValue ?? translation.Key;
   }

   private static ITranslationToolsClient ResolveClient()
   {
      return _client ?? throw new InvalidOperationException("Translations is not initialized. Call app.InitializeTranslationToolsClientAsync() during startup.");
   }
}
