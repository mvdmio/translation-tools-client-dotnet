using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace mvdmio.TranslationTools.Client.Internal;

internal static class ApplicationDependencyList
{
   private const string DEPS_FILES_PROPERTY = "APP_CONTEXT_DEPS_FILES";

   private const char DEPS_FILES_SEPARATOR = ';';

   internal static HashSet<string>? ReadDeclaredAssemblyNames()
   {
      if (AppContext.GetData(DEPS_FILES_PROPERTY) is not string files)
         return null;

      return ReadDeclaredAssemblyNames(files.Split(DEPS_FILES_SEPARATOR, StringSplitOptions.RemoveEmptyEntries));
   }

   internal static HashSet<string>? ReadDeclaredAssemblyNames(IEnumerable<string> depsFilePaths)
   {
      HashSet<string>? names = null;

      foreach (var path in depsFilePaths)
      {
         try
         {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            names ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRuntimeAssemblyNames(document.RootElement, names);
         }
         catch (Exception)
         {
         }
      }

      return names;
   }

   private static void AddRuntimeAssemblyNames(JsonElement root, HashSet<string> names)
   {
      if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
         return;

      foreach (var target in targets.EnumerateObject())
      {
         foreach (var library in target.Value.EnumerateObject())
         {
            if (!library.Value.TryGetProperty("runtime", out var runtime) || runtime.ValueKind != JsonValueKind.Object)
               continue;

            foreach (var file in runtime.EnumerateObject())
               names.Add(Path.GetFileNameWithoutExtension(file.Name));
         }
      }
   }
}
