using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace mvdmio.TranslationTools.Tool.Configuration;

internal static class ToolConfigurationLoader
{
   public static bool TryLoad(out ToolConfiguration config)
   {
      var configFilePath = FindConfigFile();

      config = configFilePath is null ? new ToolConfiguration() : LoadFromPath(configFilePath);
      ApplyApiKeyFallback(config);
      return configFilePath is not null;
   }

   public static ToolConfiguration Load()
   {
      TryLoad(out var config);
      return config;
   }

   private static ToolConfiguration LoadFromPath(string configFilePath)
   {
      var yaml = File.ReadAllText(configFilePath);
      var deserializer = new DeserializerBuilder()
         .WithNamingConvention(CamelCaseNamingConvention.Instance)
         .IgnoreUnmatchedProperties()
         .Build();

      var config = deserializer.Deserialize<ToolConfiguration>(yaml) ?? new ToolConfiguration();
      config.ConfigDirectory = Path.GetDirectoryName(configFilePath)!;
      return config;
   }

   private static void ApplyApiKeyFallback(ToolConfiguration config)
   {
      if (!string.IsNullOrWhiteSpace(config.ApiKey))
         return;

      var environmentKey = Environment.GetEnvironmentVariable(ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE);
      config.ApiKey = string.IsNullOrWhiteSpace(environmentKey) ? null : environmentKey;
   }

   private static string? FindConfigFile()
   {
      var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

      while (directory is not null)
      {
         var configPath = Path.Combine(directory.FullName, ToolConfiguration.CONFIG_FILE_NAME);
         if (File.Exists(configPath))
            return configPath;

         directory = directory.Parent;
      }

      return null;
   }
}
