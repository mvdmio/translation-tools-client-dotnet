using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Ordered locale snapshot with origin-aware lookup helpers.
/// </summary>
public sealed class TranslationLocaleSnapshot : IReadOnlyDictionary<string, string?>
{
   private readonly Lazy<IReadOnlyDictionary<TranslationRef, string?>> _index;

   internal TranslationLocaleSnapshot(string locale, IReadOnlyList<TranslationItemResponse> items)
   {
      Locale = CultureInfo.GetCultureInfo(Internal.TranslationClientInputValidator.NormalizeLocale(locale));
      Items = items;
      _index = new Lazy<IReadOnlyDictionary<TranslationRef, string?>>(
         () => new ReadOnlyDictionary<TranslationRef, string?>(
            Items.ToDictionary(static item => new TranslationRef(item.Origin, item.Key), static item => item.Value)
         )
      );
   }

   /// <summary>
   /// Snapshot locale.
   /// </summary>
   public CultureInfo Locale { get; }

   /// <summary>
   /// Ordered locale items.
   /// </summary>
   public IReadOnlyList<TranslationItemResponse> Items { get; }

   /// <summary>
   /// Check whether the snapshot contains a translation.
   /// </summary>
   public bool Contains(TranslationRef translation)
   {
      return _index.Value.ContainsKey(translation);
   }

   /// <summary>
   /// Try to read a translation value.
   /// </summary>
   public bool TryGetValue(TranslationRef translation, out string? value)
   {
      return _index.Value.TryGetValue(translation, out value);
   }

   /// <summary>
   /// Get a translation value by origin-aware reference.
   /// </summary>
   public string? this[TranslationRef translation] => _index.Value.TryGetValue(translation, out var value) ? value : null;

   /// <summary>
   /// Get a translation value using the legacy default-origin key surface.
   /// </summary>
   public string? this[string key] => this[new TranslationRef("/Localizations.resx", key)];

   /// <summary>
   /// Legacy keys from the default <c>/Localizations.resx</c> origin.
   /// </summary>
   public IEnumerable<string> Keys => Items.Where(static item => string.Equals(item.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
      .Select(static item => item.Key);

   /// <summary>
   /// Legacy values from the default <c>/Localizations.resx</c> origin.
   /// </summary>
   public IEnumerable<string?> Values => Items.Where(static item => string.Equals(item.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
      .Select(static item => item.Value);

   /// <summary>
   /// Number of legacy entries from the default <c>/Localizations.resx</c> origin.
   /// </summary>
   public int Count => Items.Count(static item => string.Equals(item.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase));

   /// <summary>
   /// Check whether the legacy default-origin surface contains a key.
   /// </summary>
   public bool ContainsKey(string key)
   {
      return Contains(new TranslationRef("/Localizations.resx", key));
   }

   /// <summary>
   /// Try to read a value from the legacy default-origin surface.
   /// </summary>
   public bool TryGetValue(string key, out string? value)
   {
      return TryGetValue(new TranslationRef("/Localizations.resx", key), out value);
   }

   /// <summary>
   /// Enumerate legacy default-origin entries in snapshot order.
   /// </summary>
   public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
   {
      return Items.Where(static item => string.Equals(item.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
         .Select(static item => new KeyValuePair<string, string?>(item.Key, item.Value))
         .GetEnumerator();
   }

   System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
   {
      return GetEnumerator();
   }

   internal static TranslationLocaleSnapshot FromItems(string locale, IEnumerable<TranslationItemResponse> items)
   {
      return new TranslationLocaleSnapshot(
         locale,
         items.OrderBy(static item => item.Origin, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray()
      );
   }
}
