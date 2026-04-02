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
            DefaultLocale = "en"
         };

         configuration.Save(directory);

         var path = Path.Combine(directory, ToolConfiguration.CONFIG_FILE_NAME);
         File.Exists(path).Should().BeTrue();

         var yaml = File.ReadAllText(path);
           yaml.Should().Contain("apiKey: project-api-key");
           yaml.Should().Contain("defaultLocale: en");
       }
      finally
      {
         if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
      }
   }

   [Fact]
   public void Load_ShouldResolveConfigDirectoryFromNearestConfigFile()
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
            "apiKey: project-api-key\ndefaultLocale: en\n"
         );

         Directory.SetCurrentDirectory(nestedDirectory);

          var configuration = ToolConfigurationLoader.Load();

          configuration.ConfigDirectory.Should().Be(configDirectory);
          configuration.DefaultLocale.Should().Be("en");
       }
      finally
      {
         Directory.SetCurrentDirectory(originalCurrentDirectory);

         if (Directory.Exists(rootDirectory))
            Directory.Delete(rootDirectory, recursive: true);
      }
   }
}
