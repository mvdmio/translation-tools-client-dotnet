using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Pull;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class TranslationSnapshotFileWriterTests
{
   [Fact]
   public void Write_ShouldSerializeIndentedJsonWithTrailingNewline()
   {
      var snapshot = new TranslationSnapshotFile
      {
         SchemaVersion = 1,
         Project = new TranslationSnapshotProject
         {
            DefaultLocale = "en",
            Locales = ["en", "nl"]
         },
         Translations = new Dictionary<string, IReadOnlyCollection<TranslationSnapshotItemFile>>
         {
            ["en"] =
            [
               new TranslationSnapshotItemFile
               {
                  Origin = "/Localizations.resx",
                  Key = "Button.Save",
                  Value = "Save"
               }
            ]
         }
      };

      var json = new TranslationSnapshotFileWriter().Write(snapshot);

      json.Should().EndWith(Environment.NewLine);
      json.Should().Contain("\"schemaVersion\": 1");

      using var document = JsonDocument.Parse(json);
      document.RootElement.GetProperty("project").GetProperty("defaultLocale").GetString().Should().Be("en");
      document.RootElement.GetProperty("translations").GetProperty("en")[0].GetProperty("key").GetString().Should().Be("Button.Save");
   }
}
