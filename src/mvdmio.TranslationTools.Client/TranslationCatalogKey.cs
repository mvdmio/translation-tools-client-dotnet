using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// One translation key from the application's <c>.resx</c> catalog, with its Neutral value and sibling-locale values.
/// </summary>
public sealed class TranslationCatalogKey
{
   private static readonly IReadOnlyDictionary<string, string> EmptyLocaleValues =
      new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

   /// <summary>
   /// Create a catalog key.
   /// </summary>
   /// <param name="origin">Origin (<c>&lt;project&gt;:&lt;path&gt;</c>).</param>
   /// <param name="key">Translation key name.</param>
   /// <param name="neutralValue">Value from the unsuffixed <c>.resx</c>, or null when absent or empty.</param>
   /// <param name="localeValues">Non-empty sibling-locale values keyed by locale name.</param>
   public TranslationCatalogKey(
      string origin,
      string key,
      string? neutralValue = null,
      IReadOnlyDictionary<string, string>? localeValues = null)
   {
      Translation = new TranslationRef(origin, key);
      NeutralValue = string.IsNullOrEmpty(neutralValue) ? null : neutralValue;
      LocaleValues = localeValues is null || localeValues.Count == 0
         ? EmptyLocaleValues
         : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(localeValues, StringComparer.OrdinalIgnoreCase));
   }

   /// <summary>
   /// Origin and translation key.
   /// </summary>
   public TranslationRef Translation { get; }

   /// <summary>
   /// Origin, written as <c>&lt;project&gt;:&lt;path&gt;</c>.
   /// </summary>
   public string Origin => Translation.Origin;

   /// <summary>
   /// Translation key name.
   /// </summary>
   public string Key => Translation.Key;

   /// <summary>
   /// Neutral value from the unsuffixed <c>.resx</c>, or null when absent or empty.
   /// </summary>
   public string? NeutralValue { get; }

   /// <summary>
   /// Non-empty sibling-locale values keyed by locale name.
   /// </summary>
   public IReadOnlyDictionary<string, string> LocaleValues { get; }
}
