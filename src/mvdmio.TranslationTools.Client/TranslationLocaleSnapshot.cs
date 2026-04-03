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
   private readonly Lazy<IReadOnlyDictionary<string, string?>> _legacyIndex;

   internal TranslationLocaleSnapshot(string locale, IReadOnlyList<TranslationItemResponse> items)
   {
      Locale = CultureInfo.GetCultureInfo(Internal.TranslationClientInputValidator.NormalizeLocale(locale));
      Items = items;
      _index = new Lazy<IReadOnlyDictionary<TranslationRef, string?>>(
         () => new ReadOnlyDictionary<TranslationRef, string?>(
            Items.ToDictionary(static item => new TranslationRef(item.Origin, item.Key), static item => item.Value)
         )
      );
      _legacyIndex = new Lazy<IReadOnlyDictionary<string, string?>>(
         () => new ReadOnlyDictionary<string, string?>(
            Items.Where(static item => string.Equals(item.Origin, "/Localizations.resx", StringComparison.OrdinalIgnoreCase))
               .ToDictionary(static item => item.Key, static item => item.Value)
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

   public string? this[TranslationRef translation] => _index.Value.TryGetValue(translation, out var value) ? value : null;

   public string? this[string key] => this[new TranslationRef("/Localizations.resx", key)];

   public IEnumerable<string> Keys => _legacyIndex.Value.Keys;

   public IEnumerable<string?> Values => _legacyIndex.Value.Values;

   public int Count => _legacyIndex.Value.Count;

   public bool ContainsKey(string key)
   {
      return _legacyIndex.Value.ContainsKey(key);
   }

   public bool TryGetValue(string key, out string? value)
   {
      return _legacyIndex.Value.TryGetValue(key, out value);
   }

   public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
   {
      return _legacyIndex.Value.GetEnumerator();
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
