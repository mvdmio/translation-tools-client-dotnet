using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Commands;
using System.CommandLine;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Commands;

public class PullCommandTests
{
   [Fact]
   public void Parse_WithPruneOption_HasNoErrors()
   {
      var parseResult = RootCommandFactory.Create().Parse(["pull", "--prune"]);

      parseResult.Errors.Should().BeEmpty();
   }

   [Theory]
   [InlineData("--bogus")]
   [InlineData("--prnue")]
   public void Parse_WithUnknownOption_ReportsOneUnrecognizedError(string option)
   {
      var parseResult = RootCommandFactory.Create().Parse(["pull", option]);

      parseResult.Errors.Should().ContainSingle()
         .Which.Message.Should().Be($"Unrecognized command or argument '{option}'.");
   }

   [Fact]
   public void Help_ForPull_DoesNotListPruneOption()
   {
      var output = new StringWriter();

      RootCommandFactory.Create().Parse(["pull", "--help"]).Invoke(new InvocationConfiguration { Output = output });

      output.ToString().Should().Contain("Pull translations from the API into local .resx files")
         .And.NotContain("--prune");
   }
}
