using AwesomeAssertions;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationClientTypesTests
{
   [Fact]
   public void TranslationRef_ShouldNormalizeOriginAndCompareOriginsCaseInsensitively()
   {
      var left = new TranslationRef("Fixture.App:\\Feature\\Shared.resx", "Button.Save");
      var right = new TranslationRef("fixture.app:/feature/shared.resx", "Button.Save");

      left.Origin.Should().Be("Fixture.App:/Feature/Shared.resx");
      left.Should().Be(right);
      left.GetHashCode().Should().Be(right.GetHashCode());
   }

   [Fact]
   public void TranslationRef_ShouldRejectInvalidOrigin()
   {
      var act = () => new TranslationRef("Feature/Shared.txt", "Button.Save");

      act.Should().Throw<ArgumentException>().WithMessage("*Translation origin must use '<project>:<path>' format.*");
   }

   [Fact]
   public void TranslationCatalogKey_ShouldHoldTranslationRefAndExposeOriginAsProjectPath()
   {
      var key = new TranslationCatalogKey("Fixture.App:\\Feature\\Shared.resx", "Button.Save", neutralValue: "Save");

      key.Translation.Should().Be(new TranslationRef("Fixture.App:/Feature/Shared.resx", "Button.Save"));
      key.Origin.Should().Be("Fixture.App:/Feature/Shared.resx");
      key.Key.Should().Be("Button.Save");
   }

   [Fact]
   public void TranslationCatalog_Register_ReplacesByTranslationRefEquality()
   {
      TranslationCatalog.Replace(
      [
         new TranslationCatalogKey("Fixture.App:/Localizations.resx", "Button.Save", neutralValue: "Save")
      ]);

      try
      {
         TranslationCatalog.Register(
            new TranslationCatalogKey("fixture.app:/localizations.resx", "Button.Save", neutralValue: "Updated")
         );

         var entry = TranslationCatalog.Entries.Should().ContainSingle().Subject;
         entry.Translation.Should().Be(new TranslationRef("Fixture.App:/Localizations.resx", "Button.Save"));
         entry.NeutralValue.Should().Be("Updated");
      }
      finally
      {
         TranslationCatalog.Clear();
      }
   }

   [Fact]
   public void TranslationLocaleSnapshot_ShouldExposeOnlyOriginAwareLookup()
   {
      var snapshot = new TranslationLocaleSnapshot(
         "EN",
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef("Fixture.App:/Feature/Shared.resx", "Button.Save")] = "Feature save",
            [new TranslationRef("Fixture.App:/Localizations.resx", "Button.Save")] = "Default save",
            [new TranslationRef("Fixture.App:/Localizations.resx", "Button.Cancel")] = null
         }
      );

      snapshot.Locale.Name.Should().Be("en");
      snapshot.Values.ContainsKey(new TranslationRef("Fixture.App:/Feature/Shared.resx", "Button.Save")).Should().BeTrue();
      snapshot.Values[new TranslationRef("Fixture.App:/Feature/Shared.resx", "Button.Save")].Should().Be("Feature save");
      snapshot.Values[new TranslationRef("Fixture.App:/Localizations.resx", "Button.Save")].Should().Be("Default save");
      snapshot.TryGetValue(new TranslationRef("Fixture.App:/Localizations.resx", "Button.Cancel"), out var cancelValue).Should().BeTrue();
      cancelValue.Should().BeNull();
      snapshot.Values.Count.Should().Be(3);
   }
}
