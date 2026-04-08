using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Scaffolding;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Scaffolding;

public class ManifestPropertyNameResolverTests
{
   [Theory]
   [InlineData("Button.Save", "Button_Save")]
   [InlineData("Button_Save", "Button_Save")]
   [InlineData("button-save.now", "ButtonSave_Now")]
   [InlineData("123.start", "Key_123_Start")]
   [InlineData("...", "Key")]
   public void Resolve_ShouldProduceStablePropertyNames(string key, string expected)
   {
      ManifestPropertyNameResolver.Resolve(key).Should().Be(expected);
   }
}
