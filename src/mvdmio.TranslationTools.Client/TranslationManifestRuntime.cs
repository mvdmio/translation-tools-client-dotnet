using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Runtime access to compiled `.resx` resources and registered client caches.
/// </summary>
public static class TranslationManifestRuntime
{
   private static readonly ConcurrentDictionary<Assembly, ITranslationToolsClient> Clients = new();
   private static readonly ConcurrentDictionary<Assembly, CultureInfo> DefaultLocales = new();
   private static readonly ConcurrentDictionary<Type, ResourceManager?> ResourceManagers = new();

   /// <summary>
   /// Register a runtime client for generated resource lookups in the given assembly.
   /// </summary>
   public static void RegisterClient(Assembly assembly, ITranslationToolsClient client)
   {
      Clients[assembly] = client;
   }

   internal static void RegisterDefaultLocale(Assembly assembly, string defaultLocale)
   {
      if (string.IsNullOrWhiteSpace(defaultLocale))
         return;

      DefaultLocales[assembly] = CultureInfo.GetCultureInfo(defaultLocale);
   }

   internal static void UnregisterClient(Assembly assembly)
   {
      Clients.TryRemove(assembly, out _);
      DefaultLocales.TryRemove(assembly, out _);
   }

   /// <summary>
   /// Get a translation for the current UI culture.
   /// </summary>
   public static string Get(Type manifestType, string key, string? defaultValue = null)
   {
      return Get(manifestType, key, CultureInfo.CurrentUICulture, defaultValue);
   }

   /// <summary>
   /// Get a translation for a specific locale.
   /// </summary>
   public static string Get(Type manifestType, string key, CultureInfo locale, string? defaultValue = null)
   {
      key = Internal.TranslationClientInputValidator.ValidateKey(key);
      locale = ResolveLocale(manifestType.Assembly, locale);

      if (TryGetCachedClientValue(manifestType.Assembly, locale, key, out var cachedValue))
         return cachedValue ?? defaultValue ?? key;

      if (TryGetResourceValue(manifestType, locale, key, out var value))
         return value ?? defaultValue ?? key;

      return defaultValue ?? key;
   }

   /// <summary>
   /// Get a translation asynchronously for the current UI culture.
   /// </summary>
   public static Task<string> GetAsync(Type manifestType, string key, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return GetAsync(manifestType, key, CultureInfo.CurrentUICulture, defaultValue, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale.
   /// </summary>
   public static async Task<string> GetAsync(Type manifestType, string key, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      key = Internal.TranslationClientInputValidator.ValidateKey(key);
      locale = ResolveLocale(manifestType.Assembly, locale);

      if (TryGetCachedClientValue(manifestType.Assembly, locale, key, out var cachedValue))
         return cachedValue ?? defaultValue ?? key;

      if (TryGetResourceValue(manifestType, locale, key, out var localValue))
         return localValue ?? defaultValue ?? key;

      if (!Clients.TryGetValue(manifestType.Assembly, out var client))
         throw new InvalidOperationException($"Translation client is not configured for assembly '{manifestType.Assembly.GetName().Name}'. Register AddTranslationToolsClient(...) and initialize the app before using generated async translation APIs.");

      var result = await client.GetAsync(key, locale, cancellationToken);
      return result.Value ?? defaultValue ?? key;
   }

   private static bool TryGetResourceValue(Type manifestType, CultureInfo locale, string key, out string? value)
   {
      var resourceManager = ResourceManagers.GetOrAdd(manifestType, static type => CreateResourceManager(type));
      if (resourceManager is null)
      {
         value = null;
         return false;
      }

      try
      {
         value = resourceManager.GetString(key, locale);
         return value is not null;
      }
      catch (MissingManifestResourceException)
      {
         value = null;
         return false;
      }
   }

   private static ResourceManager? CreateResourceManager(Type manifestType)
   {
      try
      {
         return new ResourceManager(manifestType.FullName ?? manifestType.Name, manifestType.Assembly);
      }
      catch (MissingManifestResourceException)
      {
         return null;
      }
   }

   private static CultureInfo ResolveLocale(Assembly assembly, CultureInfo locale)
   {
      if (!string.IsNullOrWhiteSpace(locale.Name))
         return locale;

      if (DefaultLocales.TryGetValue(assembly, out var defaultLocale))
         return defaultLocale;

      return CultureInfo.GetCultureInfo("en");
   }

   private static bool TryGetCachedClientValue(Assembly assembly, CultureInfo locale, string key, out string? value)
   {
      if (Clients.TryGetValue(assembly, out var client))
      {
         var cached = client.TryGetCached(key, locale);
         if (cached is not null)
         {
            value = cached.Value;
            return true;
         }
      }

      value = null;
      return false;
   }
}
