using System;
using System.Text.RegularExpressions;

namespace mvdmio.TranslationTools.Client.Internal;

internal static partial class TranslationClientInputValidator
{
   public static string NormalizeLocale(string locale)
   {
      if (string.IsNullOrWhiteSpace(locale))
         throw new ArgumentException("Locale is required.", nameof(locale));

      return locale.Trim().ToLowerInvariant();
   }

   public static string ValidateKey(string key)
   {
      if (string.IsNullOrWhiteSpace(key) || !KeyPattern().IsMatch(key))
         throw new ArgumentException("Translation key contains invalid characters.", nameof(key));

      return key;
   }

   [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
   private static partial Regex KeyPattern();
}
