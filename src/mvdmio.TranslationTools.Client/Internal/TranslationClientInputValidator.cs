using System;
using System.Text.RegularExpressions;

namespace mvdmio.TranslationTools.Client.Internal;

internal static partial class TranslationClientInputValidator
{
   private const int MaxEnvironmentLength = 64;
   private const string EnvironmentRulesMessage =
      "Environment may only contain letters, numbers, dots, underscores, and hyphens; must be at most 64 characters; and must not be '.' or '..'.";

   public static string NormalizeLocale(string locale)
   {
      if (string.IsNullOrWhiteSpace(locale))
         throw new ArgumentException("Locale is required.", nameof(locale));

      return locale.Trim().ToLowerInvariant();
   }

   /// <summary>
   /// Trims a configured Environment name and checks it against the API rule. Blank or whitespace
   /// means the unnamed Environment and returns <c>null</c>. Does not lowercase; the server does.
   /// </summary>
   public static string? NormalizeEnvironment(string? environment, string parameterName)
   {
      if (string.IsNullOrWhiteSpace(environment))
         return null;

      var normalized = environment.Trim();
      if (normalized.Length > MaxEnvironmentLength || normalized is "." or ".." || !CharsetPattern().IsMatch(normalized))
         throw new ArgumentException(EnvironmentRulesMessage, parameterName);

      return normalized;
   }

   public static string ValidateKey(string key)
   {
      if (string.IsNullOrWhiteSpace(key))
         throw new ArgumentException("Translation key is required.", nameof(key));

      var normalized = key.Trim();
      if (!CharsetPattern().IsMatch(normalized))
         throw new ArgumentException("Translation key may only contain letters, numbers, dots, underscores, and hyphens.", nameof(key));

      return normalized;
   }

   public static string ValidateOrigin(string origin)
   {
      if (string.IsNullOrWhiteSpace(origin))
         throw new ArgumentException("Translation origin is required.", nameof(origin));

      var normalized = origin.Trim();
      var separatorIndex = normalized.IndexOf(':');
      if (separatorIndex <= 0 || separatorIndex != normalized.LastIndexOf(':'))
         throw new ArgumentException($"Translation origin must use '<project>:<path>' format. Actual: {normalized}", nameof(origin));

      var projectName = normalized[..separatorIndex].Trim();
      var resourcePath = normalized[(separatorIndex + 1)..].Trim().Replace("\\", "/");

      if (string.IsNullOrWhiteSpace(projectName) || projectName.Contains(':'))
         throw new ArgumentException($"Translation origin project name must be non-empty and must not contain ':'. Actual: {normalized}", nameof(origin));

      if (!resourcePath.StartsWith('/') || !resourcePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         throw new ArgumentException($"Translation origin path must start with '/' and end with '.resx'. Actual: {resourcePath}", nameof(origin));

      return projectName + ":" + resourcePath;
   }

   [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
   private static partial Regex CharsetPattern();
}
