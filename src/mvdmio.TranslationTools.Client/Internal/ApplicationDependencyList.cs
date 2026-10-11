using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Reads the assembly names the application declares as dependencies from its <c>.deps.json</c> files.
/// The build writes a project reference there even when the compiler dropped it from the assembly
/// references because no code uses it.
/// </summary>
internal static class ApplicationDependencyList
{
   private const string DEPS_FILES_PROPERTY = "APP_CONTEXT_DEPS_FILES";

   // The host separates the paths with a semicolon on every platform.
   private const char DEPS_FILES_SEPARATOR = ';';

   /// <summary>
   /// Reads the declared dependencies of the running application.
   /// </summary>
   /// <returns>The assembly names, or <c>null</c> when the application has no readable dependency file.</returns>
   internal static HashSet<string>? ReadDeclaredAssemblyNames()
   {
      if (AppContext.GetData(DEPS_FILES_PROPERTY) is not string files)
         return null;

      return ReadDeclaredAssemblyNames(files.Split(DEPS_FILES_SEPARATOR, StringSplitOptions.RemoveEmptyEntries));
   }

   /// <summary>
   /// Reads the declared dependencies from the given <c>.deps.json</c> files.
   /// </summary>
   /// <param name="depsFilePaths">The paths of the dependency files.</param>
   /// <returns>The assembly names, or <c>null</c> when none of the files could be read.</returns>
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
            // Missing or malformed dependency file: it contributes nothing.
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
