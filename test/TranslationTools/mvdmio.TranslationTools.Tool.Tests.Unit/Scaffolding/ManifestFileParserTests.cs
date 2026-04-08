using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Scaffolding;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Scaffolding;

public class ManifestFileParserTests
{
   [Fact]
   public void ParseDocument_ShouldReturnOnlyPartialPropertiesForRequestedClass()
   {
      var content = """
         namespace Demo;

         public static partial class Localizations
         {
            [mvdmio.TranslationTools.Client.Translation(Key = "Button.Save", DefaultValue = "Save")]
            public static partial string SaveLabel { get; }

            public static string Ignored { get; } = "x";
         }

         public static partial class Other
         {
            [mvdmio.TranslationTools.Client.Translation(DefaultValue = "Cancel")]
            public static partial string CancelLabel { get; }
         }
         """;

      var result = new ManifestFileParser().ParseDocument(content, "Localizations");

      result.ClassDeclaration.Should().NotBeNull();
      result.Properties.Should().ContainSingle();
      result.Properties.Single().PropertyName.Should().Be("SaveLabel");
      result.Properties.Single().Key.Should().Be("Button.Save");
      result.Properties.Single().EmitExplicitKey.Should().BeTrue();
      result.Properties.Single().DefaultValue.Should().Be("Save");
   }

   [Fact]
   public void Parse_ShouldFallbackToPropertyName_WhenAttributeHasNoExplicitKey()
   {
      var content = """
         public static partial class Localizations
         {
            [Translation(DefaultValue = "Cancel")]
            public static partial string CancelLabel { get; }
         }
         """;

      var result = new ManifestFileParser().Parse(content, "Localizations");

      result.Should().ContainSingle();
      result.Single().Key.Should().Be("CancelLabel");
      result.Single().EmitExplicitKey.Should().BeFalse();
      result.Single().DefaultValue.Should().Be("Cancel");
   }

   [Fact]
   public void ParseDocument_ShouldReturnEmptyResult_WhenClassIsMissing()
   {
      var result = new ManifestFileParser().ParseDocument("public class Demo { }", "Localizations");

      result.ClassDeclaration.Should().BeNull();
      result.Properties.Should().BeEmpty();
   }
}
