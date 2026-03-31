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

public static class TranslationManifestRuntime
{
   private const string SNAPSHOT_RESOURCE_NAME = ".mvdmio-translations.snapshot.json";

   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
      PropertyNameCaseInsensitive = true
   };

   private static readonly ConcurrentDictionary<Assembly, TranslationAssemblySnapshot?> SnapshotCache = new();
   private static readonly ConcurrentDictionary<Assembly, ITranslationToolsClient> Clients = new();

   public static void RegisterClient(Assembly assembly, ITranslationToolsClient client)
   {
      Clients[assembly] = client;
   }

   public static string Get(Type manifestType, string key, string? defaultValue = null)
   {
      return Get(manifestType, key, CultureInfo.CurrentUICulture, defaultValue);
   }

   public static string Get(Type manifestType, string key, CultureInfo locale, string? defaultValue = null)
   {
      key = Internal.TranslationClientInputValidator.ValidateKey(key);

      if (TryGetSnapshotValue(manifestType.Assembly, locale, key, out var value))
         return value ?? defaultValue ?? key;

      return defaultValue ?? key;
   }

   public static async Task<string> GetAsync(Type manifestType, string key, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      return await GetAsync(manifestType, key, CultureInfo.CurrentUICulture, defaultValue, cancellationToken);
   }

   public static async Task<string> GetAsync(Type manifestType, string key, CultureInfo locale, string? defaultValue = null, CancellationToken cancellationToken = default)
   {
      key = Internal.TranslationClientInputValidator.ValidateKey(key);

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

   private static bool TryGetSnapshotValue(Assembly assembly, CultureInfo locale, string key, out string? value)
   {
      var snapshot = GetSnapshot(assembly);
      if (snapshot is not null && snapshot.TryGetValue(locale.Name, key, out value))
         return true;

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

   private TranslationAssemblySnapshot(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> translations)
   {
      _translations = translations;
   }

   public static TranslationAssemblySnapshot FromResource(TranslationSnapshotResource snapshot)
   {
      return new TranslationAssemblySnapshot(snapshot.Translations);
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
