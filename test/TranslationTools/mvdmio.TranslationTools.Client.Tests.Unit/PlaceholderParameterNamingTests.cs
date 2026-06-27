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
      // The collision comparison key is the @-escaped identifier with the escape stripped, so a keyword
      // token compares equal to a hypothetical same-named non-keyword token (the genuine-collision guard).
      PlaceholderParameterNaming.ParameterKey("class").Should().Be("class");
      PlaceholderParameterNaming.ParameterKey("userName").Should().Be("userName");
   }

   [Fact]
   public void FindCollision_ReturnsNull_ForDistinctTokens()
   {
      // Under the real token grammar every distinct token yields a distinct parameter identifier, so no
      // collision is possible. The guard exists defensively; this locks the no-false-positive contract.
      PlaceholderParameterNaming.FindCollision(["userName", "orderCount", "class", "n", "a1"]).Should().BeNull();
   }

   [Fact]
   public void FindCollision_DetectsCoalescingParameterIdentifiers()
   {
      // If two genuinely distinct token strings ever map to the same parameter key, the diagnostic path fires.
      var collision = PlaceholderParameterNaming.FindCollision(["count", "@count"]);

      collision.Should().NotBeNull();
      collision!.Value.Parameter.Should().Be("count");
      collision.Value.First.Should().Be("count");
      collision.Value.Second.Should().Be("@count");
   }
}
