using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using mvdmio.TranslationTools.Client.Placeholders;

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

   /// <summary>
   /// Get a translation and apply placeholder substitution. Used by generated accessors that have key-scoped tokens.
   /// </summary>
   /// <param name="translation">The translation reference.</param>
   /// <param name="defaultValue">Fallback value seeded to the server when missing.</param>
   /// <param name="localeValues">Per-locale values seeded to the server when missing.</param>
   /// <param name="bindings">Token name -> supplied value.</param>
   /// <param name="knownSet">Key-scoped token names ∪ declared global names. Unknown tokens stay inert.</param>
   public static string GetWithPlaceholders(TranslationRef translation, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet)
   {
      var value = Get(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues);
      return PlaceholderRuntime.Apply(value, bindings, knownSet);
   }

   /// <summary>
   /// Get a translation for a specific locale and apply placeholder substitution.
   /// </summary>
   public static string GetWithPlaceholders(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet)
   {
      var value = Get(translation, locale, defaultValue, localeValues);
      return PlaceholderRuntime.Apply(value, bindings, knownSet);
   }

   /// <summary>
   /// Get a translation asynchronously and apply placeholder substitution.
   /// </summary>
   public static async Task<string> GetWithPlaceholdersAsync(TranslationRef translation, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet, CancellationToken cancellationToken = default)
   {
      var value = await GetAsync(translation, CultureInfo.CurrentUICulture, defaultValue, localeValues, cancellationToken);
      return PlaceholderRuntime.Apply(value, bindings, knownSet);
   }

   /// <summary>
   /// Get a translation asynchronously for a specific locale and apply placeholder substitution.
   /// </summary>
   public static async Task<string> GetWithPlaceholdersAsync(TranslationRef translation, CultureInfo locale, string? defaultValue, IReadOnlyDictionary<string, string?>? localeValues, IReadOnlyDictionary<string, string?>? bindings, IReadOnlyCollection<string>? knownSet, CancellationToken cancellationToken = default)
   {
      var value = await GetAsync(translation, locale, defaultValue, localeValues, cancellationToken);
      return PlaceholderRuntime.Apply(value, bindings, knownSet);
   }

   /// <summary>
   /// Start a fluent placeholder build for a dynamic key lookup. Bindings are validated at render time
   /// against the fetched value (string-keyed path: knownSet = null, so every unbound/unregistered token degrades).
   /// </summary>
   /// <example>
   /// <code>Translations.WithPlaceholders(myRef).SetPlaceholder("userName", name).Render();</code>
   /// </example>
   public static PlaceholderBuilder WithPlaceholders(TranslationRef translation, string? defaultValue = null)
   {
      return new PlaceholderBuilder(translation, defaultValue);
   }

   private static ITranslationToolsClient ResolveClient()
   {
      return _client ?? throw new InvalidOperationException("Translations is not initialized. Call app.InitializeTranslationToolsClientAsync() during startup.");
   }
}
