using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
      var translation = CreateTranslationRef(manifestType, key);
      locale = ResolveLocale(locale, GetSnapshot(manifestType.Assembly));

        if (TryGetCachedClientValue(manifestType.Assembly, locale, translation, out var cachedValue))
           return cachedValue ?? defaultValue ?? key;

      if (TryGetSnapshotValue(manifestType.Assembly, locale, translation, out var value))
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
      var translation = CreateTranslationRef(manifestType, key);
      locale = ResolveLocale(locale, GetSnapshot(manifestType.Assembly));

      if (TryGetSnapshotValue(manifestType.Assembly, locale, translation, out var value))
         return value ?? defaultValue ?? key;

      if (!Clients.TryGetValue(manifestType.Assembly, out var client))
         throw new InvalidOperationException($"Translation client is not configured for assembly '{manifestType.Assembly.GetName().Name}'. Register AddTranslationToolsClient(...) and initialize the app before using generated async manifest APIs.");

      var result = await client.GetAsync(translation, locale, cancellationToken);
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

      if (TryGetRegisteredClientDefaultLocale(locale, out var clientDefaultLocale))
         return clientDefaultLocale;

      return CultureInfo.GetCultureInfo("en");
   }

   private static bool TryGetRegisteredClientDefaultLocale(CultureInfo locale, out CultureInfo resolvedLocale)
   {
      resolvedLocale = locale;

      if (!string.IsNullOrWhiteSpace(locale.Name))
         return false;

      foreach (var client in Clients.Values)
      {
         if (client is not TranslationToolsClient translationClient)
            continue;

         if (string.IsNullOrWhiteSpace(translationClient.DefaultLocale))
            continue;

         resolvedLocale = CultureInfo.GetCultureInfo(translationClient.DefaultLocale);
         return true;
      }

      return false;
   }

   private static bool TryGetSnapshotValue(Assembly assembly, CultureInfo locale, TranslationRef translation, out string? value)
   {
      var snapshot = GetSnapshot(assembly);
      if (snapshot is not null && snapshot.TryGetValue(locale.Name, translation, out value))
         return true;

      value = null;
      return false;
   }

   private static bool TryGetCachedClientValue(Assembly assembly, CultureInfo locale, TranslationRef translation, out string? value)
   {
      if (Clients.TryGetValue(assembly, out var client))
      {
         var cached = client.TryGetCached(translation, locale);
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

   private static TranslationRef CreateTranslationRef(Type manifestType, string key)
   {
      return new TranslationRef(ResolveOrigin(manifestType), key);
   }

   private static string ResolveOrigin(Type manifestType)
   {
      var field = manifestType.GetField("Origin", BindingFlags.Static | BindingFlags.NonPublic);
      if (field?.GetValue(null) is string origin && !string.IsNullOrWhiteSpace(origin))
         return Internal.TranslationClientInputValidator.ValidateOrigin(origin);

      return "/Localizations.resx";
   }
}

internal sealed class TranslationAssemblySnapshot
{
   private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<TranslationRef, string?>> _translations;
   private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> _legacyTranslations;

   private TranslationAssemblySnapshot(string defaultLocale, IReadOnlyDictionary<string, IReadOnlyDictionary<TranslationRef, string?>> translations, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> legacyTranslations)
   {
      DefaultLocale = Internal.TranslationClientInputValidator.NormalizeLocale(defaultLocale);
      _translations = translations;
      _legacyTranslations = legacyTranslations;
   }

   public string DefaultLocale { get; }

   public static TranslationAssemblySnapshot FromResource(TranslationSnapshotResource snapshot)
   {
      var translations = snapshot.Translations.ToDictionary(
         static locale => Internal.TranslationClientInputValidator.NormalizeLocale(locale.Key),
         static locale => (IReadOnlyDictionary<TranslationRef, string?>)locale.Value.SelectMany(static item => item.Expand()).ToDictionary(
            static item => item.Translation,
            static item => item.Value
         ),
         StringComparer.Ordinal
      );

      return new TranslationAssemblySnapshot(
         snapshot.Project.DefaultLocale,
         translations,
         translations.ToDictionary(
            static locale => locale.Key,
            static locale => (IReadOnlyDictionary<string, string?>)locale.Value
               .Where(static item => string.Equals(item.Key.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
               .ToDictionary(static item => item.Key.Key, static item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal
         )
      );
   }

   public bool TryGetValue(string locale, TranslationRef translation, out string? value)
   {
      var normalizedLocale = Internal.TranslationClientInputValidator.NormalizeLocale(locale);
      if (_translations.TryGetValue(normalizedLocale, out var localeItems) && localeItems.TryGetValue(translation, out value))
         return true;

      if (string.Equals(translation.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase)
         && _legacyTranslations.TryGetValue(normalizedLocale, out var legacyLocaleItems)
         && legacyLocaleItems.TryGetValue(translation.Key, out value))
         return true;

      value = null;
      return false;
   }
}

internal sealed class TranslationSnapshotResource
{
   public required int SchemaVersion { get; init; }
   public required TranslationSnapshotProjectResource Project { get; init; }
   public required IReadOnlyDictionary<string, TranslationSnapshotLocaleResource> Translations { get; init; }
}

[JsonConverter(typeof(TranslationSnapshotLocaleResourceConverter))]
internal sealed class TranslationSnapshotLocaleResource : IReadOnlyCollection<TranslationSnapshotItemResource>
{
   private readonly IReadOnlyCollection<TranslationSnapshotItemResource> _items;

   public TranslationSnapshotLocaleResource()
   {
      _items = [];
   }

   public TranslationSnapshotLocaleResource(IReadOnlyCollection<TranslationSnapshotItemResource> items)
   {
      _items = items;
   }

   public int Count => _items.Count;

   public IEnumerator<TranslationSnapshotItemResource> GetEnumerator()
   {
      return _items.GetEnumerator();
   }

   System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
   {
      return GetEnumerator();
   }
}

internal sealed class TranslationSnapshotLocaleResourceConverter : JsonConverter<TranslationSnapshotLocaleResource>
{
   public override TranslationSnapshotLocaleResource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
   {
      if (reader.TokenType == JsonTokenType.StartArray)
      {
         var items = JsonSerializer.Deserialize<TranslationSnapshotItemResource[]>(ref reader, options) ?? [];
         return new TranslationSnapshotLocaleResource(items);
      }

      if (reader.TokenType != JsonTokenType.StartObject)
         throw new JsonException("Expected translation locale payload to be an array or object.");

      var itemsFromObject = new List<TranslationSnapshotItemResource>();
      while (reader.Read())
      {
         if (reader.TokenType == JsonTokenType.EndObject)
            return new TranslationSnapshotLocaleResource(itemsFromObject);

         var key = reader.GetString() ?? throw new JsonException("Snapshot key is required.");
         reader.Read();
         string? value = reader.TokenType == JsonTokenType.Null ? null : JsonSerializer.Deserialize<string>(ref reader, options);
         itemsFromObject.Add(new TranslationSnapshotItemResource {
            Origin = "/Localizations.resx",
            Key = key,
            Value = value
         });
      }

      throw new JsonException("Unexpected end of translation locale payload.");
   }

   public override void Write(Utf8JsonWriter writer, TranslationSnapshotLocaleResource value, JsonSerializerOptions options)
   {
      JsonSerializer.Serialize(writer, value.ToArray(), options);
   }
}

internal sealed class TranslationSnapshotItemResource
{
   public string? Origin { get; init; }
   public required string Key { get; init; }
   public required string? Value { get; init; }

   public IEnumerable<(TranslationRef Translation, string? Value)> Expand()
   {
      yield return (new TranslationRef(Origin ?? "/Localizations.resx", Key), Value);
   }
}

internal sealed class TranslationSnapshotProjectResource
{
   public required string DefaultLocale { get; init; }
   public required IReadOnlyCollection<string> Locales { get; init; }
}
