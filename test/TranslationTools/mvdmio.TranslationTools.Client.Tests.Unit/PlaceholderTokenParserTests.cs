using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Placeholders;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class PlaceholderTokenParserTests
{
   [Theory]
   [InlineData("Hello {userName}!", new[] { "userName" })]
   [InlineData("{a} {b} {a}", new[] { "a", "b" })]
   [InlineData("No tokens here", new string[0])]
   [InlineData("{0} {Key} {x_y}", new string[0])]
   [InlineData("Order {orderCount} of {orderCount}", new[] { "orderCount" })]
   [InlineData("It's {n}", new[] { "n" })]
   [InlineData("'{'userName'}'", new string[0])]
   [InlineData("'{userName}'", new string[0])]
   [InlineData("Cost '{'total'}' is {amount}", new[] { "amount" })]
   public void TokenNames_ShouldMatchSpecVectors(string input, string[] expected)
   {
      PlaceholderTokenParser.TokenNames(input).Should().Equal(expected);
   }

   [Fact]
   public void Parse_ShouldResolveLiteralBraceEscapes()
   {
      var segments = PlaceholderTokenParser.Parse("'{'literal'}'");

      segments.Should().ContainSingle();
      segments[0].IsToken.Should().BeFalse();
      segments[0].Text.Should().Be("{literal}");
   }

   [Fact]
   public void Parse_ShouldTreatDoubledApostropheAsLiteral()
   {
      var segments = PlaceholderTokenParser.Parse("It''s {n}");

      segments[0].IsToken.Should().BeFalse();
      segments[0].Text.Should().Be("It's ");
      segments[1].IsToken.Should().BeTrue();
      segments[1].Text.Should().Be("n");
   }
}
