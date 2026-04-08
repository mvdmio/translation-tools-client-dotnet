using System.Collections.Generic;
using System.Globalization;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Locale snapshot with origin-aware lookup helpers.
/// </summary>
public sealed class TranslationLocaleSnapshot
{
   internal TranslationLocaleSnapshot(string locale, IReadOnlyDictionary<TranslationRef, string?> values)
   {
      Locale = CultureInfo.GetCultureInfo(Internal.TranslationClientInputValidator.NormalizeLocale(locale));
      Values = values;
   }

   /// <summary>
   /// Snapshot locale.
   /// </summary>
   public CultureInfo Locale { get; }

   /// <summary>
   /// Origin-aware lookup dictionary for this locale.
   /// </summary>
   public IReadOnlyDictionary<TranslationRef, string?> Values { get; }

   /// <summary>
   /// Check whether the snapshot contains a translation.
   /// </summary>
   public bool Contains(TranslationRef translation)
   {
      return Values.ContainsKey(translation);
   }

   /// <summary>
   /// Try to read a translation value.
   /// </summary>
   public bool TryGetValue(TranslationRef translation, out string? value)
   {
      return Values.TryGetValue(translation, out value);
   }
}
