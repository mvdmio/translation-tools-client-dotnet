using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Runtime access to embedded translation snapshots and registered client caches.
/// </summary>
public static class TranslationManifestRuntime
{
   private const string SNAPSHOT_RESOURCE_NAME = ".mvdmio-translations.snapshot.json";

   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
      PropertyNameCaseInsensitive = true
   };

   private static readonly ConcurrentDictionary<Assembly, TranslationAssemblySnapshot?> SnapshotCache = new();
   private static readonly ConcurrentDictionary<Assembly, ITranslationToolsClient> Clients = new();

   /// <summary>
   /// Register a runtime client for generated manifest lookups in the given assembly.
   /// </summary>
   public static void RegisterClient(Assembly assembly, ITranslationToolsClient client)
   {
      Clients[assembly] = client;
   }

   internal static void UnregisterClient(Assembly assembly)
   {
      Clients.TryRemove(assembly, out _);
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
      locale = ResolveLocale(locale, GetSnapshot(manifestType.Assembly));

       if (TryGetCachedClientValue(manifestType.Assembly, locale, key, out var cachedValue))
          return cachedValue ?? defaultValue ?? key;

      if (TryGetSnapshotValue(manifestType.Assembly, locale, key, out var value))
         return value ?? defaultValue ?? key;

      return defaultValue ?? key;
   }

   /// <summary>
   /// Get a translation asynchronously for the current UI culture.
   /// </summary>
   public static async Task<string> GetAsync(Type manifestType, string key, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return await GetAsync(manifestType, key, CultureInfo.CurrentUICulture, defaultValue, cancellationToken);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale.
   /// </summary>
   public static async Task<string> GetAsync(Type manifestType, string key, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      key = Internal.TranslationClientInputValidator.ValidateKey(key);
      locale = ResolveLocale(locale, GetSnapshot(manifestType.Assembly));

      if (TryGetSnapshotValue(manifestType.Assembly, locale, key, out var value))
         return value ?? defaultValue ?? key;

      if (!Clients.TryGetValue(manifestType.Assembly, out var client))
         throw new InvalidOperationException($"Translation client is not configured for assembly '{manifestType.Assembly.GetName().Name}'. Register AddTranslationToolsClient(...) and initialize the app before using generated async manifest APIs.");

      var result = await client.GetAsync(key, locale, cancellationToken);
      return result.Value ?? defaultValue ?? key;
   }

   internal static TranslationAssemblySnapshot? GetSnapshot(Assembly assembly)
   {
      return SnapshotCache.GetOrAdd(assembly, LoadSnapshot);
   }

   private static CultureInfo ResolveLocale(CultureInfo locale, TranslationAssemblySnapshot? snapshot)
   {
      if (!string.IsNullOrWhiteSpace(locale.Name))
         return locale;

      if (!string.IsNullOrWhiteSpace(snapshot?.DefaultLocale))
         return CultureInfo.GetCultureInfo(snapshot.DefaultLocale);

      return CultureInfo.GetCultureInfo("en");
   }

   private static bool TryGetSnapshotValue(Assembly assembly, CultureInfo locale, string key, out string? value)
   {
      var snapshot = GetSnapshot(assembly);
      if (snapshot is not null && snapshot.TryGetValue(locale.Name, key, out value))
         return true;

      value = null;
      return false;
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

   private static TranslationAssemblySnapshot? LoadSnapshot(Assembly assembly)
   {
      var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(static x => x.EndsWith(SNAPSHOT_RESOURCE_NAME, StringComparison.Ordinal));
      if (resourceName is null)
         return null;

      using var stream = assembly.GetManifestResourceStream(resourceName);

      if (stream is null)
         return null;

      var snapshot = JsonSerializer.Deserialize<TranslationSnapshotResource>(stream, SerializerOptions);
      return snapshot is null ? null : TranslationAssemblySnapshot.FromResource(snapshot);
   }
}

internal sealed class TranslationAssemblySnapshot
{
   private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> _translations;

   private TranslationAssemblySnapshot(string defaultLocale, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> translations)
   {
      DefaultLocale = Internal.TranslationClientInputValidator.NormalizeLocale(defaultLocale);
      _translations = translations;
   }

   public string DefaultLocale { get; }

   public static TranslationAssemblySnapshot FromResource(TranslationSnapshotResource snapshot)
   {
      return new TranslationAssemblySnapshot(snapshot.Project.DefaultLocale, snapshot.Translations);
   }

   public bool TryGetValue(string locale, string key, out string? value)
   {
      var normalizedLocale = Internal.TranslationClientInputValidator.NormalizeLocale(locale);
      if (_translations.TryGetValue(normalizedLocale, out var localeItems) && localeItems.TryGetValue(key, out value))
         return true;

      value = null;
      return false;
   }
}

internal sealed class TranslationSnapshotResource
{
   public required int SchemaVersion { get; init; }
   public required TranslationSnapshotProjectResource Project { get; init; }
   public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Translations { get; init; }
}

internal sealed class TranslationSnapshotProjectResource
{
   public required string DefaultLocale { get; init; }
   public required IReadOnlyCollection<string> Locales { get; init; }
}
