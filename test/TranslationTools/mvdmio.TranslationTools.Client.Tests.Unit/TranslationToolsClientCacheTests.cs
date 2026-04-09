using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationToolsClientCacheTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public async Task Cache_ShouldStoreRetrieveAndRemoveTypedEntries()
   {
      var cache = new LocalTranslationToolsClientCache();
      var locale = CultureInfo.GetCultureInfo("en").Name;
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      var entry = new TranslationToolsClientCacheEntry<TranslationItemResponse>
      {
         Value = new TranslationItemResponse
         {
            Origin = translation.Origin,
            Key = translation.Key,
            Value = "value"
         }
      };

      await cache.SetAsync(locale, entry, TestContext.Current.CancellationToken);

      cache.Get(locale, translation)!.Value.Value.Should().Be("value");
      (await cache.GetAsync(locale, translation, TestContext.Current.CancellationToken))!.Value.Value.Should().Be("value");
      cache.GetLocale(locale).Should().BeNull();

      await cache.RemoveAsync(locale, translation, TestContext.Current.CancellationToken);

      cache.Get(locale, translation).Should().BeNull();
   }

   [Fact]
   public async Task Cache_SetLocale_ShouldPopulateLocaleAndTranslationLookups()
   {
      var cache = new LocalTranslationToolsClientCache();
      var locale = CultureInfo.GetCultureInfo("en").Name;
      var snapshot = new TranslationLocaleSnapshot(
         locale,
         new Dictionary<TranslationRef, string?>
         {
            [new TranslationRef(ProjectOriginPrefix + "/Feature/Shared.resx", "Button.Save")] = "Feature save",
            [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")] = "Cancel"
         }
      );

      await cache.SetLocaleAsync(locale, new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot> { Value = snapshot }, TestContext.Current.CancellationToken);

      cache.GetLocale(locale)!.Value.Values[new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")].Should().Be("Cancel");
      cache.Get(locale, new TranslationRef(ProjectOriginPrefix + "/Feature/Shared.resx", "Button.Save"))!.Value.Value.Should().Be("Feature save");
   }

   [Fact]
   public async Task Cache_SetTranslation_ShouldRefreshExistingLocaleSnapshot()
   {
      var cache = new LocalTranslationToolsClientCache();
      var locale = CultureInfo.GetCultureInfo("en").Name;

      await cache.SetLocaleAsync(
         locale,
         new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>
         {
            Value = new TranslationLocaleSnapshot(
               locale,
               new Dictionary<TranslationRef, string?>
               {
                  [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")] = "Save"
               }
            )
         },
         TestContext.Current.CancellationToken
      );

      await cache.SetAsync(
         locale,
         new TranslationToolsClientCacheEntry<TranslationItemResponse>
         {
            Value = new TranslationItemResponse
            {
               Origin = ProjectOriginPrefix + "/Localizations.resx",
               Key = "Button.Save",
               Value = "Save now"
            }
         },
         TestContext.Current.CancellationToken
      );

      cache.GetLocale(locale)!.Value.Values[new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")].Should().Be("Save now");
   }

   [Fact]
   public async Task Cache_RemoveTranslation_ShouldRefreshExistingLocaleSnapshot()
   {
      var cache = new LocalTranslationToolsClientCache();
      var locale = CultureInfo.GetCultureInfo("en").Name;
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      await cache.SetLocaleAsync(
         locale,
         new TranslationToolsClientCacheEntry<TranslationLocaleSnapshot>
         {
            Value = new TranslationLocaleSnapshot(
               locale,
               new Dictionary<TranslationRef, string?>
               {
                  [translation] = "Save",
                  [new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")] = "Cancel"
               }
            )
         },
         TestContext.Current.CancellationToken
      );

      await cache.RemoveAsync(locale, translation, TestContext.Current.CancellationToken);

      cache.Get(locale, translation).Should().BeNull();
      cache.GetLocale(locale)!.Value.Values.ContainsKey(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save")).Should().BeFalse();
      cache.GetLocale(locale)!.Value.Values.ContainsKey(new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Cancel")).Should().BeTrue();
   }
}
