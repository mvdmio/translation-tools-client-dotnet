using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Configuration;

public class ToolPathResolverTests
{
   [Fact]
   public void GetOutputPath_WithProjectContext_ShouldPreferConfiguredOutput()
   {
      var config = new ToolConfiguration
      {
         ConfigDirectory = "C:\\repo",
         Output = Path.Combine("Generated", "Texts.g.cs"),
         ClassName = "Ignored"
      };

      var path = ToolPathResolver.GetOutputPath(
         config,
         new ToolProjectContext
         {
            ProjectDirectory = "C:\\repo\\src\\Demo",
            ProjectFilePath = "C:\\repo\\src\\Demo\\Demo.csproj",
            RootNamespace = "Demo"
         }
      );

      path.Should().Be(Path.GetFullPath(Path.Combine("C:\\repo\\src\\Demo", "Generated", "Texts.g.cs")));
   }

   [Fact]
   public void GetOutputPath_WithoutConfiguredOutput_ShouldUseClassName()
   {
      var path = ToolPathResolver.GetOutputPath(
         new ToolConfiguration
         {
            ConfigDirectory = "C:\\repo",
            ClassName = "Localizations"
         }
      );

      path.Should().Be(Path.GetFullPath(Path.Combine("C:\\repo", "Localizations.cs")));
   }
}
