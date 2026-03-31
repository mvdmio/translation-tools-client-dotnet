using mvdmio.TranslationTools.Client;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace mvdmio.TranslationTools.Tool.Configuration;

internal sealed class ToolConfiguration
{
   public const string CONFIG_FILE_NAME = ".mvdmio-translations.yml";
   public const string SNAPSHOT_FILE_NAME = ".mvdmio-translations.snapshot.json";
   internal const string DEFAULT_BASE_URL = "https://translations.mvdm.io";

   [YamlIgnore]
   public string ConfigDirectory { get; set; } = Directory.GetCurrentDirectory();
   [YamlIgnore]
   internal string? SharedKeyPrefix { get; set; }
   public string? ApiKey { get; set; }
   public string? DefaultLocale { get; set; }
   public string? Output { get; set; }
   public string? Namespace { get; set; }
   public string ClassName { get; set; } = "Localizations";
   public TranslationKeyNaming KeyNaming { get; set; } = TranslationKeyNaming.UnderscoreToDot;

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
