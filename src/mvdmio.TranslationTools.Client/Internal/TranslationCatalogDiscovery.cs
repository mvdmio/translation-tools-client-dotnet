using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace mvdmio.TranslationTools.Client.Internal;

internal static class TranslationCatalogDiscovery
{
   private static readonly string _clientAssemblyName = typeof(TranslationCatalog).Assembly.GetName().Name!;

   internal static void RegisterAllAssemblies(string? applicationDirectory = null, IReadOnlySet<string>? declaredAssemblyNames = null)
   {
      var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var pending = new Queue<Assembly>();

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
         Enqueue(assembly);

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
