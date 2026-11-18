using mvdmio.TranslationTools.Client.Internal;
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
   private static readonly object _gate = new();
   private static Dictionary<TranslationRef, TranslationCatalogKey> _entries = new();

   /// <summary>
   /// All registered catalog keys across loaded assemblies, in registration order.
   /// </summary>
   public static IReadOnlyList<TranslationCatalogKey> Entries
   {
      get
      {
         lock (_gate)
            return _entries.Values.ToArray();
      }
   }

   internal static IReadOnlyList<TranslationCatalogKey> DiscoverEntries()
   {
      TranslationCatalogDiscovery.RegisterAllAssemblies();

      return Entries;
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

      lock (_gate)
      {
         foreach (var key in keys)
         {
            ArgumentNullException.ThrowIfNull(key);
            _entries[key.Translation] = key;
         }
      }
   }

   internal static void Clear()
   {
      lock (_gate)
         _entries = new Dictionary<TranslationRef, TranslationCatalogKey>();
   }

   internal static void Replace(IEnumerable<TranslationCatalogKey> keys)
   {
      ArgumentNullException.ThrowIfNull(keys);

      var next = new Dictionary<TranslationRef, TranslationCatalogKey>();
      foreach (var key in keys)
      {
         ArgumentNullException.ThrowIfNull(key);
         next[key.Translation] = key;
      }

      lock (_gate)
         _entries = next;
   }
}
