using System;
using System.Collections.Generic;
using System.Text;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal static class PlaceholderSubstitution
{
   public static string Substitute(
      string value,
      IReadOnlyDictionary<string, string?>? bindings,
      IGlobalPlaceholderResolver globals,
      IReadOnlyCollection<string>? knownSet,
      bool throwOnError,
      Action<string>? warn = null)
   {
      if (string.IsNullOrEmpty(value))
         return value;

      if (value.IndexOf('{') < 0 && value.IndexOf('\'') < 0 && (bindings is null || bindings.Count == 0))
         return value;

      var segments = PlaceholderTokenParser.Parse(value);

      var builder = new StringBuilder(value.Length);
      var consumedTokens = bindings is { Count: > 0 } ? new HashSet<string>(StringComparer.Ordinal) : null;

      foreach (var segment in segments)
      {
         if (!segment.IsToken)
         {
            builder.Append(segment.Text);
            continue;
         }

         var name = segment.Text;

         if (bindings is not null && bindings.TryGetValue(name, out var bound))
         {
            builder.Append(bound ?? string.Empty);
            consumedTokens?.Add(name);
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

         if (knownSet is not null && !System.Linq.Enumerable.Contains(knownSet, name))
         {
            builder.Append('{').Append(name).Append('}');
            continue;
         }

         Degrade(builder, name, throwOnError, warn, $"No value supplied for placeholder '{{{name}}}'.");
      }

      if (bindings is { Count: > 0 } && consumedTokens is not null)
      {
         foreach (var key in bindings.Keys)
         {
            if (!consumedTokens.Contains(key))
               WarnOrThrow(throwOnError, warn, $"Supplied placeholder '{key}' is not present in the value.");
         }
      }

      return builder.ToString();
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
