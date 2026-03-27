using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Static translation facade for application-wide access.
/// </summary>
public static class Translate
{
   private static TranslationToolsClient? _client;

   private static TranslationToolsClient Client => _client ?? throw new InvalidOperationException("Translate is not configured. Call services.UseTranslationToolsClient();");
   
   /// <summary>
   /// Configure the static facade with a client instance.
   /// </summary>
   public static void Configure(TranslationToolsClient client)
   {
      _client = client;
   }

   /// <summary>
   /// Initialize the configured client.
   /// </summary>
   public static async Task InitializeAsync(CancellationToken cancellationToken = default)
   {
      await Client.Initialize(cancellationToken);
   }

   /// <summary>
   /// Get a translation from the local cache using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public static string Get(string key, string? defaultValue = null)
   {
      return Get(key, CultureInfo.CurrentUICulture, defaultValue);
   }

   /// <summary>
   /// Get a translation from the local cache using an explicit locale.
   /// </summary>
   public static string Get(string key, CultureInfo locale, string? defaultValue = null)
   {
      return _client?.TryGetCached(key, locale)?.Value ?? defaultValue ?? key;
   }

   /// <summary>
   /// Get a translation using <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public static async Task<string> GetAsync(string key, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return (await Client.GetAsync(key, CultureInfo.CurrentUICulture, defaultValue, cancellationToken)).Value ?? defaultValue ?? key;
   }

   /// <summary>
   /// Get a translation using an explicit locale.
   /// </summary>
   public static async Task<string> GetAsync(string key, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return (await Client.GetAsync(key, locale, defaultValue, cancellationToken)).Value ?? defaultValue ?? key;
   }
}
