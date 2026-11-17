using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Commands;
using System.CommandLine;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Commands;

public class PushCommandTests
{
   [Fact]
   public void Parse_WithPruneOption_HasNoErrors()
   {
      var parseResult = RootCommandFactory.Create().Parse(["push", "--prune"]);

      parseResult.Errors.Should().BeEmpty();
   }

   [Theory]
   [InlineData("--bogus")]
   [InlineData("--prnue")]
   public void Parse_WithUnknownOption_ReportsOneUnrecognizedError(string option)
   {
      var parseResult = RootCommandFactory.Create().Parse(["push", option]);

      parseResult.Errors.Should().ContainSingle()
         .Which.Message.Should().Be($"Unrecognized command or argument '{option}'.");
   }

   [Fact]
   public void Help_ForPush_DoesNotListPruneOption()
   {
      var output = new StringWriter();

      RootCommandFactory.Create().Parse(["push", "--help"]).Invoke(new InvocationConfiguration { Output = output });

      output.ToString().Should().Contain("Push project translation keys to the API")
         .And.NotContain("--prune");
   }
}
