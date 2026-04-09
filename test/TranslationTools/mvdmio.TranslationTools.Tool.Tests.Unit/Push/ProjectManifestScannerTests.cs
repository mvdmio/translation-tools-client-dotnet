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
   public void ScanProject_ShouldThrow_WhenNoResxFilesExist()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         var act = () => new ProjectManifestScanner().ScanProject(projectDirectory, "en");

         act.Should().Throw<InvalidOperationException>().WithMessage($"*No .resx locale files found in project '{projectDirectory}'.*");
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
