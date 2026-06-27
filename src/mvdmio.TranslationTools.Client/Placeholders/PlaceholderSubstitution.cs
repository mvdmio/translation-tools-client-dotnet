using System;
using System.Collections.Generic;
using System.Text;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Runtime placeholder substitution engine (spec §5). Pure string substitution for Phase 1.
/// </summary>
internal static class PlaceholderSubstitution
{
   /// <summary>
   /// Substitute placeholder tokens in <paramref name="value"/>.
   /// </summary>
   /// <param name="value">The raw translation value (may contain tokens).</param>
   /// <param name="bindings">Per-call bindings. A binding shadows a global of the same name.</param>
   /// <param name="globals">Ambient global resolver.</param>
   /// <param name="knownSet">
   /// Generator path: <c>keyScopedNames ∪ declaredGlobalNames</c>. Unknown tokens become inert literals (no warn).
   /// String-keyed path: <c>null</c>, so every unbound/unregistered token degrades (raw + warn).
   /// </param>
   /// <param name="throwOnError">When true, every warn case throws <see cref="PlaceholderSubstitutionException"/> instead.</param>
   /// <param name="warn">Sink for warning messages (no-op when null).</param>
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

      // Fast path: nothing to do when there is no token/escape marker and no bindings to validate. A '{' may
      // open a token and a '\'' may open an ICU escape the parser collapses, so either forces the full pass;
      // this matches the KMP client's render() short-circuit and keeps the common no-placeholder render cheap.
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

         // 1. Binding wins (shadows a global of the same name).
         if (bindings is not null && bindings.TryGetValue(name, out var bound))
         {
            builder.Append(bound ?? string.Empty);
            consumedTokens?.Add(name);
            continue;
         }

         // 2. Registered global -> resolve ambiently.
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

         // 3. knownSet provided and name not in it -> inert literal, no warning (US29).
         if (knownSet is not null && !System.Linq.Enumerable.Contains(knownSet, name))
         {
            builder.Append('{').Append(name).Append('}');
            continue;
         }

         // 4. Managed token with no supplied value -> degrade.
         Degrade(builder, name, throwOnError, warn, $"No value supplied for placeholder '{{{name}}}'.");
      }

      // Extra supplied binding that never appeared as a token -> warn, still succeed.
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
