using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Push;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Push;

public class PushHandlerTests
{
   public class ProjectManifestScannerTests
   {
      [Fact]
      public void ScanProject_ShouldCollectKeysAndDefaultValuesFromTranslationManifests()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(
               Path.Combine(projectDirectory, "Localizations.cs"),
               """
               using mvdmio.TranslationTools.Client;

               namespace Demo;

               [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
               public static partial class Localizations
               {
                  [Translation(DefaultValue = "Save")]
                  public static partial string Action_Save { get; }

                  [Translation(Key = "legacy.button.cancel", DefaultValue = "Cancel")]
                  public static partial string Action_Cancel { get; }
               }
               """
            );

            var result = new ProjectManifestScanner().ScanProject(projectDirectory);

            result.FoundManifest.Should().BeTrue();
            result.Items.Should().HaveCount(2);
            result.Items.Should().ContainSingle(x => x.Key == "Action.Save" && x.DefaultValue == "Save");
            result.Items.Should().ContainSingle(x => x.Key == "legacy.button.cancel" && x.DefaultValue == "Cancel");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ScanProject_ShouldPreferNonEmptyDefaultValueForDuplicateKeys()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(
               Path.Combine(projectDirectory, "A.cs"),
               """
               using mvdmio.TranslationTools.Client;

               namespace Demo;

               [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
               public static partial class One
               {
                  public static partial string Action_Save { get; }
               }
               """
            );

            File.WriteAllText(
               Path.Combine(projectDirectory, "B.cs"),
               """
               using mvdmio.TranslationTools.Client;

               namespace Demo;

               [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
               public static partial class Two
               {
                  [Translation(DefaultValue = "Save")]
                  public static partial string Action_Save { get; }
               }
               """
            );

            var result = new ProjectManifestScanner().ScanProject(projectDirectory);

            result.Items.Should().ContainSingle(x => x.Key == "Action.Save" && x.DefaultValue == "Save");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ScanProject_ShouldThrowForConflictingDefaultValues()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(
               Path.Combine(projectDirectory, "A.cs"),
               """
               using mvdmio.TranslationTools.Client;

               namespace Demo;

               [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
               public static partial class One
               {
                  [Translation(DefaultValue = "Save")]
                  public static partial string Action_Save { get; }
               }
               """
            );

            File.WriteAllText(
               Path.Combine(projectDirectory, "B.cs"),
               """
               using mvdmio.TranslationTools.Client;

               namespace Demo;

               [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
               public static partial class Two
               {
                  [Translation(DefaultValue = "Opslaan")]
                  public static partial string Action_Save { get; }
               }
               """
            );

            var action = () => new ProjectManifestScanner().ScanProject(projectDirectory);

            action.Should().Throw<InvalidOperationException>().WithMessage("*Action.Save*");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }
   }

   public class ResolveProjectDirectory
   {
      [Fact]
      public void ShouldResolveNearestCsprojFromOutputPath()
      {
         var rootDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
         var projectDirectory = Path.Combine(rootDirectory, "src", "Demo");

         try
         {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Localization"));

            var result = PushHandler.ResolveProjectDirectory(
               new ToolConfiguration {
                  ConfigDirectory = rootDirectory,
                  Output = Path.Combine("src", "Demo", "Localization", "Localizations.cs")
               }
            );

            result.Should().Be(projectDirectory);
         }
         finally
         {
            if (Directory.Exists(rootDirectory))
               Directory.Delete(rootDirectory, recursive: true);
         }
      }
   }

   private static string CreateTempProject()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);
      File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
      return projectDirectory;
   }
}
