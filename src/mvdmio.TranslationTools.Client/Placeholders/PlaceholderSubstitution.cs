using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal static class PlaceholderSubstitution
{
   private const string MISSING_NUMBER_MESSAGE = "No number supplied for the Plural in this value.";

   public static string Substitute(
      string value,
      IReadOnlyDictionary<string, string?>? bindings,
      IGlobalPlaceholderResolver globals,
      IReadOnlyCollection<string>? knownSet,
      long? pluralNumber,
      EffectiveLocale locale,
      bool throwOnError,
      Action<string>? warn = null)
   {
      if (string.IsNullOrEmpty(value))
         return value;

      if (value.IndexOf('{') < 0 && value.IndexOf('\'') < 0 && (bindings is null || bindings.Count == 0))
         return value;

      IReadOnlyList<PlaceholderTokenParser.Segment> segments;
      IEnumerable<PlaceholderTokenParser.Segment> presentSegments;
      string? argument = null;
      var numberText = string.Empty;

      if (PluralParser.TryParse(value) is { } plural)
      {
         if (pluralNumber is null)
            WarnOrThrow(throwOnError, warn, MISSING_NUMBER_MESSAGE);

         segments = plural.SelectForm(pluralNumber, locale.Name).Segments;
         presentSegments = plural.AllSegments;
         argument = plural.Argument;
         numberText = pluralNumber is { } number ? FormatNumber(number, locale) : "#";
      }
      else
      {
         var parsed = PlaceholderTokenParser.Parse(value);
         segments = parsed;
         presentSegments = parsed;
      }

      var builder = new StringBuilder(value.Length);

      foreach (var segment in segments)
      {
         if (segment.IsNumber)
         {
            builder.Append(numberText);
            continue;
         }

         if (!segment.IsToken)
         {
            builder.Append(segment.Text);
            continue;
         }

         var name = segment.Text;

         if (name == argument)
         {
            builder.Append(numberText);
            continue;
         }

         if (bindings is not null && bindings.TryGetValue(name, out var bound))
         {
            builder.Append(bound ?? string.Empty);
            continue;
         }

         if (globals.IsRegistered(name))
         {
            if (globals.TryResolve(name, out var resolved) && resolved is not null)
            {
               builder.Append(resolved);
            }
            else
            {
               Degrade(builder, name, throwOnError, warn, $"Could not resolve global placeholder '{{{name}}}' (outside scope, resolver failed, or value was null).");
            }

            continue;
         }

         if (knownSet is not null && !knownSet.Contains(name))
         {
            builder.Append('{').Append(name).Append('}');
            continue;
         }

         Degrade(builder, name, throwOnError, warn, $"No value supplied for placeholder '{{{name}}}'.");
      }

      if (bindings is { Count: > 0 })
      {
         var presentTokens = presentSegments.Where(s => s.IsToken).Select(s => s.Text).ToHashSet(StringComparer.Ordinal);

         foreach (var key in bindings.Keys)
         {
            if (!presentTokens.Contains(key))
               WarnOrThrow(throwOnError, warn, $"Supplied placeholder '{key}' is not present in the value.");
         }
      }

      return builder.ToString();
   }

   private static string FormatNumber(long number, EffectiveLocale locale)
   {
      CultureInfo culture;

      try
      {
         culture = CultureInfo.GetCultureInfo(locale.Name);
      }
      catch (CultureNotFoundException)
      {
         culture = CultureInfo.InvariantCulture;
      }

      return number.ToString("N0", culture);
   }

   private static void Degrade(StringBuilder builder, string name, bool throwOnError, Action<string>? warn, string message)
   {
      if (throwOnError)
         throw new PlaceholderSubstitutionException(message);

      warn?.Invoke(message);
      builder.Append('{').Append(name).Append('}');
   }

   private static void WarnOrThrow(bool throwOnError, Action<string>? warn, string message)
   {
      if (throwOnError)
         throw new PlaceholderSubstitutionException(message);

      warn?.Invoke(message);
   }
}
