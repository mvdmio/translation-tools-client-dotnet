using AwesomeAssertions;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationClientTypesTests
{
   [Fact]
   public void TranslationRef_ShouldNormalizeOriginAndCompareOriginsCaseInsensitively()
   {
      var left = new TranslationRef("\\Feature\\Shared.resx", "Button.Save");
      var right = new TranslationRef("/feature/shared.resx", "Button.Save");

      left.Origin.Should().Be("/Feature/Shared.resx");
      left.Should().Be(right);
      left.GetHashCode().Should().Be(right.GetHashCode());
   }

   [Fact]
   public void TranslationRef_ShouldRejectInvalidOrigin()
   {
      var act = () => new TranslationRef("Feature/Shared.txt", "Button.Save");

      act.Should().Throw<ArgumentException>().WithMessage("*Translation origin must start with '/' and end with '.resx'.*");
   }

   [Fact]
   public void TranslationLocaleSnapshot_ShouldExposeLegacyLookupOnlyForDefaultOrigin()
   {
      var snapshot = TranslationLocaleSnapshot.FromItems(
         "EN",
         [
            new TranslationItemResponse
            {
               Origin = "/Feature/Shared.resx",
               Key = "Button.Save",
               Value = "Feature save"
            },
            new TranslationItemResponse
            {
               Origin = "/Localizations.resx",
               Key = "Button.Save",
               Value = "Default save"
            },
            new TranslationItemResponse
            {
               Origin = "/Localizations.resx",
               Key = "Button.Cancel",
               Value = null
            }
         ]
      );

      snapshot.Locale.Name.Should().Be("en");
      snapshot.Contains(new TranslationRef("/Feature/Shared.resx", "Button.Save")).Should().BeTrue();
      snapshot[new TranslationRef("/Feature/Shared.resx", "Button.Save")].Should().Be("Feature save");
      snapshot.ContainsKey("Button.Save").Should().BeTrue();
      snapshot["Button.Save"].Should().Be("Default save");
      snapshot.TryGetValue("Button.Cancel", out var cancelValue).Should().BeTrue();
      cancelValue.Should().BeNull();
      snapshot.Keys.Should().Equal("Button.Cancel", "Button.Save");
      snapshot.Values.Should().Equal((string?)null, "Default save");
      snapshot.Count.Should().Be(2);
   }
}
