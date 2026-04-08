using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Static access helpers used by generated localization classes.
/// </summary>
public static class TranslationToolsClient
{
   private static IServiceProvider? _serviceProvider;

   internal static void SetServiceProvider(IServiceProvider serviceProvider)
   {
      _serviceProvider = serviceProvider;
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
      var cached = client.TryGetCached(translation, locale);
      return cached?.Value ?? defaultValue ?? translation.Key;
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
      var item = await client.GetAsync(translation, locale, cancellationToken);
      return item.Value ?? defaultValue ?? translation.Key;
   }

   private static Internal.TranslationToolsClientRuntime ResolveClient()
   {
      var serviceProvider = _serviceProvider ?? throw new InvalidOperationException("TranslationToolsClient is not initialized. Call app.InitializeTranslationToolsClientAsync() during startup.");
      return serviceProvider.GetRequiredService<Internal.TranslationToolsClientRuntime>();
   }
}
