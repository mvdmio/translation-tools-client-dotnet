using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Push;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Push;

public class PushHandlerTests
{
   [Fact]
   public void ResolveProjectDirectory_ShouldResolveNearestCsprojFromConfigDirectory()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

      try
      {
         Directory.CreateDirectory(projectDirectory);
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
         File.WriteAllText(Path.Combine(projectDirectory, ToolConfiguration.CONFIG_FILE_NAME), "apiKey: test\ndefaultLocale: en\n");

         var result = PushHandler.ResolveProjectDirectory(
            new ToolConfiguration {
               ConfigDirectory = projectDirectory,
               DefaultLocale = "en"
            }
         );

         result.Should().Be(projectDirectory);
      }
      finally
      {
         if (Directory.Exists(projectDirectory))
            Directory.Delete(projectDirectory, recursive: true);
      }
   }
}
