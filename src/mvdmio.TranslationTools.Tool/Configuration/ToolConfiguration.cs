using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace mvdmio.TranslationTools.Tool.Configuration;

internal sealed class ToolConfiguration
{
   public const string CONFIG_FILE_NAME = ".mvdmio-translations.yml";
   internal const string DEFAULT_BASE_URL = "https://translations.mvdm.io";

   [YamlIgnore]
   public string ConfigDirectory { get; set; } = Directory.GetCurrentDirectory();
   public string? ApiKey { get; set; }
   public string? DefaultLocale { get; set; }

   public void Save(string directoryPath)
   {
      Directory.CreateDirectory(directoryPath);

      var serializer = new SerializerBuilder()
         .WithNamingConvention(CamelCaseNamingConvention.Instance)
         .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
         .Build();

      var path = Path.Combine(directoryPath, CONFIG_FILE_NAME);
      File.WriteAllText(path, serializer.Serialize(this));
   }
}
