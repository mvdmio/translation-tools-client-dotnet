using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Commands;
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
         var configuration = new ToolConfiguration
         {
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

   [Fact]
   public void Load_WhenFileHasNoApiKey_ShouldUseEnvironmentVariable()
   {
      using var scope = new ConfigScope("defaultLocale: en\n", environmentApiKey: "env-key");

      ToolConfigurationLoader.Load().ApiKey.Should().Be("env-key");
   }

   [Fact]
   public void Load_WhenFileApiKeyIsBlank_ShouldUseEnvironmentVariable()
   {
      using var scope = new ConfigScope("apiKey: \"  \"\ndefaultLocale: en\n", environmentApiKey: "env-key");

      ToolConfigurationLoader.Load().ApiKey.Should().Be("env-key");
   }

   [Fact]
   public void Load_WhenFileHasApiKey_ShouldPreferFileOverEnvironmentVariable()
   {
      using var scope = new ConfigScope("apiKey: file-key\ndefaultLocale: en\n", environmentApiKey: "env-key");

      ToolConfigurationLoader.Load().ApiKey.Should().Be("file-key");
   }

   [Fact]
   public void TryLoad_WhenNoConfigFile_ShouldCarryEnvironmentVariableKey()
   {
      using var scope = new ConfigScope(configYaml: null, environmentApiKey: "env-key");

      var found = ToolConfigurationLoader.TryLoad(out var configuration);

      found.Should().BeFalse();
      configuration.ApiKey.Should().Be("env-key");
   }

   [Theory]
   [InlineData(null)]
   [InlineData("")]
   [InlineData("   ")]
   public void Load_WhenFileHasNoApiKeyAndVariableIsUnsetOrBlank_ShouldLeaveApiKeyNull(string? environmentApiKey)
   {
      using var scope = new ConfigScope("defaultLocale: en\n", environmentApiKey);

      ToolConfigurationLoader.Load().ApiKey.Should().BeNull();
   }

   [Fact]
   public void InitRun_ShouldWriteConfigWithoutApiKeyAndMentionEnvironmentVariable()
   {
      var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(directory);

      try
      {
         var output = new StringWriter();

         InitCommand.Run(directory, output, new StringWriter());

         var yaml = File.ReadAllText(Path.Combine(directory, ToolConfiguration.CONFIG_FILE_NAME));
         yaml.Should().NotContain("apiKey");
         yaml.Should().Contain("defaultLocale: en");
         output.ToString().Should().Contain("TRANSLATIONS_API_KEY");
      }
      finally
      {
         Directory.Delete(directory, recursive: true);
      }
   }

   /// <summary>
   /// Sets the process-wide current directory and TRANSLATIONS_API_KEY for one test and restores both on dispose.
   /// The tests that use it share this class, so xunit does not run them in parallel.
   /// </summary>
   private sealed class ConfigScope : IDisposable
   {
      private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();
      private readonly string? _originalApiKey = Environment.GetEnvironmentVariable(ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE);

      public ConfigScope(string? configYaml, string? environmentApiKey)
      {
         Directory.CreateDirectory(_rootDirectory);

         if (configYaml is not null)
            File.WriteAllText(Path.Combine(_rootDirectory, ToolConfiguration.CONFIG_FILE_NAME), configYaml);

         Directory.SetCurrentDirectory(_rootDirectory);
         Environment.SetEnvironmentVariable(ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE, environmentApiKey);
      }

      public void Dispose()
      {
         Environment.SetEnvironmentVariable(ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE, _originalApiKey);
         Directory.SetCurrentDirectory(_originalCurrentDirectory);
         Directory.Delete(_rootDirectory, recursive: true);
      }
   }
}
