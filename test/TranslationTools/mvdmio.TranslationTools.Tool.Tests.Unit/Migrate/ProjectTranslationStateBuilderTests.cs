using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Migrate;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Migrate;

public class ProjectTranslationStateBuilderTests
{
   [Fact]
   public void Build_ShouldPrefixKeysForSingleResourceSetProjects()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         var filePath = Path.Combine(projectDirectory, "Errors.resx");
         File.WriteAllText(filePath, """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="title" xml:space="preserve">
                <value>Error</value>
              </data>
            </root>
            """);

         var result = new ProjectTranslationStateBuilder().Build(
            new ResxMigrationScanResult {
               HasBaseFiles = true,
               SourceFiles = [
                  new ResxMigrationSourceFile {
                     FilePath = filePath,
                     RelativePath = "Errors.resx",
                     ResourceSetPath = "Errors",
                     ResourceSetName = "Errors",
                     Locale = null
                  }
               ]
            },
            "en"
         );

         result.State.Items.Should().ContainSingle(x => x.Key == "Errors.title");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }
}
