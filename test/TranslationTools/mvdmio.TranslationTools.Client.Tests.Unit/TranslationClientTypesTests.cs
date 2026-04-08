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
   public void TranslationLocaleSnapshot_ShouldExposeOnlyOriginAwareLookup()
   {
      var snapshot = new TranslationLocaleSnapshot(
         "EN",
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef("/Feature/Shared.resx", "Button.Save")] = "Feature save",
            [new TranslationRef("/Localizations.resx", "Button.Save")] = "Default save",
            [new TranslationRef("/Localizations.resx", "Button.Cancel")] = null
         }
      );

      snapshot.Locale.Name.Should().Be("en");
      snapshot.Values.ContainsKey(new TranslationRef("/Feature/Shared.resx", "Button.Save")).Should().BeTrue();
      snapshot.Values[new TranslationRef("/Feature/Shared.resx", "Button.Save")].Should().Be("Feature save");
      snapshot.Values[new TranslationRef("/Localizations.resx", "Button.Save")].Should().Be("Default save");
      snapshot.TryGetValue(new TranslationRef("/Localizations.resx", "Button.Cancel"), out var cancelValue).Should().BeTrue();
      cancelValue.Should().BeNull();
      snapshot.Values.Count.Should().Be(3);
   }
}
