using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Configuration;

public class ToolConfigurationTests
{
   [Fact]
   public void Save_ShouldWriteYamlConfigFile()
   {
      var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

      try
      {
         var configuration = new ToolConfiguration {
            ApiKey = "project-api-key",
            Output = "Localizations.cs"
         };

         configuration.Save(directory);

         var path = Path.Combine(directory, ToolConfiguration.CONFIG_FILE_NAME);
         File.Exists(path).Should().BeTrue();

         var yaml = File.ReadAllText(path);
         yaml.Should().Contain("apiKey: project-api-key");
         yaml.Should().Contain("output: Localizations.cs");
         yaml.Should().NotContain("basePath:");
         yaml.Should().NotContain("locale:");
         yaml.Should().NotContain("includeCulture:");
         yaml.Should().NotContain("staticClass:");
      }
      finally
      {
         if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
      }
   }

   [Fact]
   public void Load_ShouldResolveOutputRelativeToConfigFileDirectory()
   {
      var rootDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      var configDirectory = Path.Combine(rootDirectory, "config-root");
      var nestedDirectory = Path.Combine(configDirectory, "src", "Feature");
      var originalCurrentDirectory = Directory.GetCurrentDirectory();

      try
      {
         Directory.CreateDirectory(nestedDirectory);
         File.WriteAllText(
            Path.Combine(configDirectory, ToolConfiguration.CONFIG_FILE_NAME),
            "apiKey: project-api-key\noutput: Localization/Localizations.cs\n"
         );

         Directory.SetCurrentDirectory(nestedDirectory);

         var configuration = ToolConfigurationLoader.Load();
         var outputPath = ToolPathResolver.GetOutputPath(configuration);

         configuration.ConfigDirectory.Should().Be(configDirectory);
         outputPath.Should().Be(Path.GetFullPath(Path.Combine(configDirectory, "Localization", "Localizations.cs")));
      }
      finally
      {
         Directory.SetCurrentDirectory(originalCurrentDirectory);

         if (Directory.Exists(rootDirectory))
            Directory.Delete(rootDirectory, recursive: true);
      }
   }
}
