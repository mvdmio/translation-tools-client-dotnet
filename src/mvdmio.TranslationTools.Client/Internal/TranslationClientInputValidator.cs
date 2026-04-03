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
      if (string.IsNullOrWhiteSpace(key))
         throw new ArgumentException("Translation key is required.", nameof(key));

      var normalized = key.Trim();
      if (!KeyPattern().IsMatch(normalized))
         throw new ArgumentException("Translation key may only contain letters, numbers, dots, underscores, and hyphens.", nameof(key));

      return normalized;
   }

   public static string ValidateOrigin(string origin)
   {
      if (string.IsNullOrWhiteSpace(origin))
         throw new ArgumentException("Translation origin is required.", nameof(origin));

      var normalized = origin.Trim().Replace("\\", "/");
      if (!normalized.StartsWith("/", StringComparison.Ordinal) || !normalized.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         throw new ArgumentException("Translation origin must start with '/' and end with '.resx'.", nameof(origin));

      return normalized;
   }

   [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
   private static partial Regex KeyPattern();
}
