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
   /// <param name="origin">Resource-set origin (<c>project:/path.resx</c>).</param>
   /// <param name="key">Translation key name.</param>
   /// <param name="neutralValue">Value from the unsuffixed <c>.resx</c>, or null when absent or empty.</param>
   /// <param name="localeValues">Non-empty sibling-locale values keyed by locale name.</param>
   public TranslationCatalogKey(
      string origin,
      string key,
      string? neutralValue = null,
      IReadOnlyDictionary<string, string>? localeValues = null)
   {
      Origin = Internal.TranslationClientInputValidator.ValidateOrigin(origin);
      Key = Internal.TranslationClientInputValidator.ValidateKey(key);
      NeutralValue = string.IsNullOrEmpty(neutralValue) ? null : neutralValue;
      LocaleValues = localeValues is null || localeValues.Count == 0
         ? EmptyLocaleValues
         : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(localeValues, StringComparer.OrdinalIgnoreCase));
   }

   /// <summary>
   /// Resource-set origin.
   /// </summary>
   public string Origin { get; }

   /// <summary>
   /// Translation key name.
   /// </summary>
   public string Key { get; }

   /// <summary>
   /// Neutral value from the unsuffixed <c>.resx</c>, or null when absent or empty.
   /// </summary>
   public string? NeutralValue { get; }

   /// <summary>
   /// Non-empty sibling-locale values keyed by locale name.
   /// </summary>
   public IReadOnlyDictionary<string, string> LocaleValues { get; }
}
