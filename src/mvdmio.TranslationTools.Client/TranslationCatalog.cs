using System;
using System.Collections.Generic;
using System.Linq;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Assembly-level registry of translation keys emitted by the source generator.
/// Generated <c>ModuleInitializer</c> code registers every <c>.resx</c> key when the application assembly loads.
/// </summary>
public static class TranslationCatalog
{
   private static readonly object Gate = new();
   private static List<TranslationCatalogKey> _entries = new();

   /// <summary>
   /// All registered catalog keys across loaded assemblies, in registration order.
   /// </summary>
   public static IReadOnlyList<TranslationCatalogKey> Entries
   {
      get
      {
         lock (Gate)
            return _entries.ToArray();
      }
   }

   /// <summary>
   /// Register catalog keys. Called from generated module initializers.
   /// A later registration for the same origin and key replaces the earlier entry.
   /// </summary>
   public static void Register(params TranslationCatalogKey[] keys)
   {
      Register((IEnumerable<TranslationCatalogKey>)keys);
   }

   /// <summary>
   /// Register catalog keys. Called from generated module initializers.
   /// A later registration for the same origin and key replaces the earlier entry.
   /// </summary>
   public static void Register(IEnumerable<TranslationCatalogKey> keys)
   {
      ArgumentNullException.ThrowIfNull(keys);

      lock (Gate)
      {
         foreach (var key in keys)
         {
            ArgumentNullException.ThrowIfNull(key);

            var index = _entries.FindIndex(existing => SameKey(existing, key));
            if (index >= 0)
               _entries[index] = key;
            else
               _entries.Add(key);
         }
      }
   }

   /// <summary>
   /// Clears all registered keys. Intended for tests.
   /// </summary>
   internal static void Clear()
   {
      lock (Gate)
         _entries = new List<TranslationCatalogKey>();
   }

   /// <summary>
   /// Replaces the entire catalog. Intended for tests.
   /// </summary>
   internal static void Replace(IEnumerable<TranslationCatalogKey> keys)
   {
      ArgumentNullException.ThrowIfNull(keys);

      lock (Gate)
         _entries = keys.ToList();
   }

   private static bool SameKey(TranslationCatalogKey left, TranslationCatalogKey right)
   {
      return StringComparer.OrdinalIgnoreCase.Equals(left.Origin, right.Origin)
             && StringComparer.Ordinal.Equals(left.Key, right.Key);
   }
}
