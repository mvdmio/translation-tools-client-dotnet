using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace mvdmio.TranslationTools.Tool.Configuration;

internal static class ToolConfigurationLoader
{
   public static bool TryLoad(out ToolConfiguration config)
   {
      var configFilePath = FindConfigFile();

      if (configFilePath is null)
      {
         config = new ToolConfiguration();
         return false;
      }

      config = LoadFromPath(configFilePath);
      return true;
   }

   public static ToolConfiguration Load()
   {
      return TryLoad(out var config) ? config : new ToolConfiguration();
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
