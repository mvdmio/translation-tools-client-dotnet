using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Finds every assembly of the application that carries generated catalog data and runs its module
/// initializer, so its keys are in <see cref="TranslationCatalog"/> whether or not code in it has run.
/// </summary>
internal static class TranslationCatalogDiscovery
{
   private static readonly string _clientAssemblyName = typeof(TranslationCatalog).Assembly.GetName().Name!;

   /// <summary>
   /// Registers the keys of every marked assembly the application depends on. Assemblies that cannot be
   /// loaded or whose initializer throws are skipped.
   /// </summary>
   /// <param name="applicationDirectory">The directory scanned for marked assemblies. Defaults to the application's base directory.</param>
   /// <param name="declaredAssemblyNames">The assembly names the application declares as dependencies. Defaults to the names in the application's <c>.deps.json</c>; the directory scan is unrestricted when the application has none.</param>
   internal static void RegisterAllAssemblies(string? applicationDirectory = null, IReadOnlySet<string>? declaredAssemblyNames = null)
   {
      var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var pending = new Queue<Assembly>();

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
         Enqueue(assembly);

      // The compiler leaves out references the code never uses, so a class library that only holds
      // .resx files can be missing from the reference graph. The application directory still holds it.
      // Only dependencies the application declares count: a library the application no longer references
      // can leave a stale DLL in the directory, and its keys must not reach the Declaration.
      var declared = declaredAssemblyNames ?? ApplicationDependencyList.ReadDeclaredAssemblyNames();
      foreach (var name in FindMarkedAssembliesInDirectory(applicationDirectory ?? AppContext.BaseDirectory, declared))
         EnqueueByName(name);

      while (pending.TryDequeue(out var assembly))
      {
         try
         {
            if (!ReferencesClient(assembly))
               continue;

            RunModuleInitializerIfMarked(assembly);

            foreach (var reference in assembly.GetReferencedAssemblies())
               EnqueueByName(reference);
         }
         catch (Exception)
         {
            // A broken assembly in the dependency graph must not fail Initialize.
         }
      }

      void Enqueue(Assembly assembly)
      {
         if (assembly.IsDynamic || !visited.Add(assembly.GetName().Name ?? assembly.FullName ?? string.Empty))
            return;

         pending.Enqueue(assembly);
      }

      void EnqueueByName(AssemblyName name)
      {
         if (name.Name is null || visited.Contains(name.Name))
            return;

         try
         {
            Enqueue(Assembly.Load(name));
         }
         catch (Exception)
         {
            // Not loadable (missing, wrong format, blocked): skip it.
         }
      }
   }

   private static bool ReferencesClient(Assembly assembly)
   {
      return assembly.GetReferencedAssemblies().Any(static reference => string.Equals(reference.Name, _clientAssemblyName, StringComparison.Ordinal));
   }

   private static void RunModuleInitializerIfMarked(Assembly assembly)
   {
      if (!assembly.IsDefined(typeof(TranslationCatalogAssemblyAttribute), inherit: false))
         return;

      RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
   }

   /// <summary>
   /// Lists the marked assemblies in a directory without loading them.
   /// </summary>
   /// <param name="directory">The directory to scan for <c>*.dll</c> files.</param>
   /// <param name="declaredAssemblyNames">When given, only assemblies with a name in this set are listed.</param>
   internal static List<AssemblyName> FindMarkedAssembliesInDirectory(string directory, IReadOnlySet<string>? declaredAssemblyNames)
   {
      var found = new List<AssemblyName>();

      string[] paths;
      try
      {
         paths = Directory.GetFiles(directory, "*.dll");
      }
      catch (Exception)
      {
         return found;
      }

      foreach (var path in paths)
      {
         if (declaredAssemblyNames is not null && !declaredAssemblyNames.Contains(Path.GetFileNameWithoutExtension(path)))
            continue;

         try
         {
            var name = ReadNameIfMarked(path);
            if (name is not null)
               found.Add(name);
         }
         catch (Exception)
         {
            // Not a managed assembly or unreadable: skip it.
         }
      }

      return found;
   }

   private static AssemblyName? ReadNameIfMarked(string path)
   {
      using var stream = File.OpenRead(path);
      using var peReader = new PEReader(stream);

      if (!peReader.HasMetadata)
         return null;

      var reader = peReader.GetMetadataReader();
      if (!reader.IsAssembly)
         return null;

      var definition = reader.GetAssemblyDefinition();

      foreach (var handle in definition.GetCustomAttributes())
      {
         var attribute = reader.GetCustomAttribute(handle);
         if (attribute.Constructor.Kind != HandleKind.MemberReference)
            continue;

         var parent = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
         if (parent.Kind != HandleKind.TypeReference)
            continue;

         var type = reader.GetTypeReference((TypeReferenceHandle)parent);
         if (reader.StringComparer.Equals(type.Name, nameof(TranslationCatalogAssemblyAttribute))
             && reader.StringComparer.Equals(type.Namespace, typeof(TranslationCatalogAssemblyAttribute).Namespace!))
         {
            return new AssemblyName(reader.GetString(definition.Name));
         }
      }

      return null;
   }
}
