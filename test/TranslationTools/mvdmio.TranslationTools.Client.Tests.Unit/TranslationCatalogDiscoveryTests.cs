using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Internal;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationCatalogDiscoveryTests
{
   // These tests scan a temporary directory, not the application directory, so they never load the
   // class library that ClassLibraryDeclarationEndToEndTests expects to be untouched before Initialize.

   [Fact]
   public void RegisterAllAssemblies_WhenDirectoryHoldsAnUnreadableDll_ShouldNotThrow()
   {
      var directory = Directory.CreateTempSubdirectory("discovery-tests-");

      try
      {
         File.WriteAllText(Path.Combine(directory.FullName, "Broken.dll"), "this is not a managed assembly");

         var act = () => TranslationCatalogDiscovery.RegisterAllAssemblies(directory.FullName);

         act.Should().NotThrow();
      }
      finally
      {
         directory.Delete(recursive: true);
      }
   }

   [Fact]
   public void FindMarkedAssembliesInDirectory_WhenMarkedAssemblyIsNotDeclared_ShouldSkipIt()
   {
      var directory = CopyMarkedLibraryToTempDirectory();

      try
      {
         var found = TranslationCatalogDiscovery.FindMarkedAssembliesInDirectory(directory.FullName, new HashSet<string> { "SomeOtherLibrary" });

         found.Should().BeEmpty();
      }
      finally
      {
         directory.Delete(recursive: true);
      }
   }

   [Fact]
   public void FindMarkedAssembliesInDirectory_WhenMarkedAssemblyIsDeclared_ShouldListIt()
   {
      var directory = CopyMarkedLibraryToTempDirectory();

      try
      {
         var found = TranslationCatalogDiscovery.FindMarkedAssembliesInDirectory(directory.FullName, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sourcegeneratorclasslibrary" });

         found.Select(name => name.Name).Should().Equal("SourceGeneratorClassLibrary");
      }
      finally
      {
         directory.Delete(recursive: true);
      }
   }

   [Fact]
   public void FindMarkedAssembliesInDirectory_WhenApplicationHasNoDeclaredDependencies_ShouldListEveryMarkedAssembly()
   {
      var directory = CopyMarkedLibraryToTempDirectory();

      try
      {
         var found = TranslationCatalogDiscovery.FindMarkedAssembliesInDirectory(directory.FullName, declaredAssemblyNames: null);

         found.Select(name => name.Name).Should().Equal("SourceGeneratorClassLibrary");
      }
      finally
      {
         directory.Delete(recursive: true);
      }
   }

   [Fact]
   public void ReadDeclaredAssemblyNames_ShouldListRuntimeAssembliesOfEveryLibraryInTheDepsFile()
   {
      var directory = Directory.CreateTempSubdirectory("discovery-tests-");

      try
      {
         var depsPath = Path.Combine(directory.FullName, "app.deps.json");
         File.WriteAllText(
            depsPath,
            """
            {
              "targets": {
                ".NETCoreApp,Version=v10.0": {
                  "App/1.0.0": { "runtime": { "App.dll": {} } },
                  "Lib/1.0.0": { "runtime": { "lib/net10.0/Lib.Core.dll": {} } },
                  "ResourcesOnly/1.0.0": { "dependencies": { "Lib": "1.0.0" } }
                }
              }
            }
            """
         );

         var names = ApplicationDependencyList.ReadDeclaredAssemblyNames([depsPath, Path.Combine(directory.FullName, "missing.deps.json")]);

         names.Should().BeEquivalentTo("App", "Lib.Core");
      }
      finally
      {
         directory.Delete(recursive: true);
      }
   }

   [Fact]
   public void ReadDeclaredAssemblyNames_WhenNoFileIsReadable_ShouldReturnNull()
   {
      var missing = Path.Combine(Path.GetTempPath(), $"discovery-tests-missing-{Guid.NewGuid():N}.deps.json");

      ApplicationDependencyList.ReadDeclaredAssemblyNames([missing]).Should().BeNull();
   }

   [Fact]
   public void ReadDeclaredAssemblyNames_OfRunningApplication_ShouldListClassLibraryThatCodeNeverUses()
   {
      ApplicationDependencyList.ReadDeclaredAssemblyNames().Should().Contain("SourceGeneratorClassLibrary");
   }

   [Fact]
   public void RegisterAllAssemblies_WhenDirectoryDoesNotExist_ShouldNotThrow()
   {
      var missing = Path.Combine(Path.GetTempPath(), $"discovery-tests-missing-{Guid.NewGuid():N}");

      var act = () => TranslationCatalogDiscovery.RegisterAllAssemblies(missing);

      act.Should().NotThrow();
   }

   private static DirectoryInfo CopyMarkedLibraryToTempDirectory()
   {
      var directory = Directory.CreateTempSubdirectory("discovery-tests-");
      File.Copy(Path.Combine(AppContext.BaseDirectory, "SourceGeneratorClassLibrary.dll"), Path.Combine(directory.FullName, "SourceGeneratorClassLibrary.dll"));

      return directory;
   }
}
