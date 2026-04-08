using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Pull;
using System.Xml.Linq;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class ResxFileWriterTests
{
   [Fact]
   public void Write_ShouldEmitXmlSpaceAttributeUsingXmlNamespace()
   {
      var writer = new ResxFileWriter();

      var xml = writer.Write(
         new ResxFileModel
         {
            FilePath = "Translations.resx",
            Entries = [
               new ResxDataEntryModel
               {
                  Key = "Button_EditStreetSegments",
                  Value = "Edit street segments"
               }
            ]
         }
      );

      var document = XDocument.Parse(xml);
      var dataElement = document.Root!.Element("data");

      dataElement.Should().NotBeNull();
      dataElement!.Attribute(XNamespace.Xml + "space")!.Value.Should().Be("preserve");
      dataElement.Attribute("name")!.Value.Should().Be("Button_EditStreetSegments");
   }
}
