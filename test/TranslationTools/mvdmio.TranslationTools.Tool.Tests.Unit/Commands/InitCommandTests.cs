using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Commands;
using mvdmio.TranslationTools.Tool.Configuration;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Commands;

public class InitCommandTests
{
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
}
