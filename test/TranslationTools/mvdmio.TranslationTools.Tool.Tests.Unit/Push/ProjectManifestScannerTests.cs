using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Migrate;
using mvdmio.TranslationTools.Tool.Push;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Push;

public class ProjectManifestScannerTests
{
   [Fact]
   public void ScanProject_ShouldIncludeMissingLocaleKeysWithNullValues()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         WriteResx(
            Path.Combine(projectDirectory, "Localizations.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="A"><value>Alpha</value></data>
              <data name="B"><value>Beta</value></data>
              <data name="C"><value>Gamma</value></data>
            </root>
            """
         );

         WriteResx(
            Path.Combine(projectDirectory, "Localizations.nl.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="A"><value>Alfa</value></data>
            </root>
            """
         );

         var result = new ProjectManifestScanner().ScanProject(projectDirectory, "en");

         result.FoundManifest.Should().BeTrue();
         result.Items.Should().HaveCount(6);
         result.Items.Should().ContainSingle(static x => x.Key == "A" && x.Locale == "en" && x.Value == "Alpha");
         result.Items.Should().ContainSingle(static x => x.Key == "B" && x.Locale == "en" && x.Value == "Beta");
         result.Items.Should().ContainSingle(static x => x.Key == "C" && x.Locale == "en" && x.Value == "Gamma");
         result.Items.Should().ContainSingle(static x => x.Key == "A" && x.Locale == "nl" && x.Value == "Alfa");
         result.Items.Should().ContainSingle(static x => x.Key == "B" && x.Locale == "nl" && x.Value == null);
         result.Items.Should().ContainSingle(static x => x.Key == "C" && x.Locale == "nl" && x.Value == null);
      }
      finally
      {
         DeleteDirectory(projectDirectory);
      }
   }

   [Fact]
   public void ScanProject_ShouldFallbackToManifestFiles_WhenNoResxFilesExist()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Localizations.cs"),
            """
            namespace Demo;

            public static partial class Localizations
            {
               [mvdmio.TranslationTools.Client.Translation(DefaultValue = "Save")]
               public static partial string Button_Save { get; }

               [mvdmio.TranslationTools.Client.Translation(Key = "Button.Cancel", DefaultValue = "Cancel")]
               public static partial string CancelLabel { get; }
            }
            """
         );

         var result = new ProjectManifestScanner().ScanProject(projectDirectory);

         result.FoundManifest.Should().BeTrue();
         result.Items.Should().HaveCount(2);
         result.Items.Should().ContainSingle(static x => x.Key == "Button_Save" && x.Locale == "en" && x.Origin == "/Localizations.resx" && x.Value == "Save");
         result.Items.Should().ContainSingle(static x => x.Key == "Button.Cancel" && x.Locale == "en" && x.Origin == "/Localizations.resx" && x.Value == "Cancel");
      }
      finally
      {
         DeleteDirectory(projectDirectory);
      }
   }

   [Fact]
   public void ScanProject_ShouldThrow_WhenManifestFilesConflictOnDefaultValue()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "First.cs"),
            """
            public static partial class Localizations
            {
               [mvdmio.TranslationTools.Client.Translation(DefaultValue = "Save")]
               public static partial string Button_Save { get; }
            }
            """
         );
         File.WriteAllText(
            Path.Combine(projectDirectory, "Second.cs"),
            """
            public static partial class Localizations
            {
               [mvdmio.TranslationTools.Client.Translation(DefaultValue = "Store")]
               public static partial string Button_Save { get; }
            }
            """
         );

         var act = () => new ProjectManifestScanner().ScanProject(projectDirectory);

         act.Should().Throw<InvalidOperationException>().WithMessage("*Conflicting default values for translation key 'Button_Save'.*");
      }
      finally
      {
         DeleteDirectory(projectDirectory);
      }
   }

   [Fact]
   public void BuildOrigin_ShouldConvertResourceSetPathToResxOrigin()
   {
      ProjectManifestScanner.BuildOrigin(
         new ResxMigrationSourceFile
         {
            FilePath = "ignored",
            RelativePath = Path.Combine("Resources", "Shared", "Messages.resx"),
            ResourceSetPath = Path.Combine("Resources", "Shared", "Messages"),
            ResourceSetName = "Resources.Shared.Messages",
            Locale = null
         }
      ).Should().Be("/Resources/Shared/Messages.resx");
   }

   private static string CreateProjectDirectory()
   {
      var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(path);
      return path;
   }

   private static void WriteResx(string path, string content)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, content);
   }

   private static void DeleteDirectory(string path)
   {
      if (Directory.Exists(path))
         Directory.Delete(path, recursive: true);
   }
}
