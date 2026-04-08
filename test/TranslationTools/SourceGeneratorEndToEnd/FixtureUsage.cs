using Fixture.App.Resources.Shared;
using mvdmio.TranslationTools.Client;
using System.Globalization;

namespace Fixture.App;

public static class FixtureUsage
{
   public static async Task<(string SyncValue, string AsyncValue, TranslationRef SaveKey, TranslationRef ErrorKey)> ExerciseAsync(IServiceProvider services)
   {
      Translations.SetServiceProvider(services);

      var syncValue = Localizations.Button_Save;
      var asyncValue = await Errors.GetAsync("404.title", new CultureInfo("en"));

      return (syncValue, asyncValue, Localizations.Keys.Button_Save, Errors.Keys._404_title);
   }
}
