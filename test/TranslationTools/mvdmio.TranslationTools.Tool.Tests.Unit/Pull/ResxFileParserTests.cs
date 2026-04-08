using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class ResxFileParserTests
{
   [Fact]
   public void Parse_ShouldSortEntriesNormalizeEmptyValuesAndKeepComments()
   {
      var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".resx");

      try
      {
         File.WriteAllText(filePath, """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Zeta"><value>Last</value></data>
              <data name="Alpha"><value></value><comment>first comment</comment></data>
              <data><value>ignored</value></data>
            </root>
            """);

         var result = new ResxFileParser().Parse(filePath);
         var entries = result.Entries.ToArray();

         result.FilePath.Should().Be(filePath);
         entries.Select(static x => x.Key).Should().Equal("Alpha", "Zeta");
         entries[0].Value.Should().BeNull();
         entries[0].Comment.Should().Be("first comment");
         entries[1].Value.Should().Be("Last");
      }
      finally
      {
         if (File.Exists(filePath))
            File.Delete(filePath);
      }
   }
}
