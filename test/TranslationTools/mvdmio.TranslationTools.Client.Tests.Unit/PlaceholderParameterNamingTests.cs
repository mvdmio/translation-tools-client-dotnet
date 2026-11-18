using AwesomeAssertions;
using mvdmio.TranslationTools.Client.SourceGenerator;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class PlaceholderParameterNamingTests
{
   [Fact]
   public void ToParameterIdentifier_EscapesKeywords()
   {
      PlaceholderParameterNaming.ToParameterIdentifier("class").Should().Be("@class");
      PlaceholderParameterNaming.ToParameterIdentifier("userName").Should().Be("userName");
   }

   [Fact]
   public void ParameterKey_StripsKeywordEscape()
   {
      PlaceholderParameterNaming.ParameterKey("class").Should().Be("class");
      PlaceholderParameterNaming.ParameterKey("userName").Should().Be("userName");
   }

   [Fact]
   public void FindCollision_ReturnsNull_ForDistinctTokens()
   {
      PlaceholderParameterNaming.FindCollision(["userName", "orderCount", "class", "n", "a1"]).Should().BeNull();
   }

   [Fact]
   public void FindCollision_DetectsCoalescingParameterIdentifiers()
   {
      var collision = PlaceholderParameterNaming.FindCollision(["count", "@count"]);

      collision.Should().NotBeNull();
      collision!.Value.Parameter.Should().Be("count");
      collision.Value.First.Should().Be("count");
      collision.Value.Second.Should().Be("@count");
   }
}
