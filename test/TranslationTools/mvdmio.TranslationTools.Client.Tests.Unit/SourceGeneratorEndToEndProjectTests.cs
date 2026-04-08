using AwesomeAssertions;
using Fixture.App;
using Fixture.App.Resources.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class SourceGeneratorEndToEndProjectTests
{
   [Fact]
   public async Task FixtureProject_ShouldGenerateCompileAndRunExpectedSurface()
   {
      typeof(Localizations).Should().NotBeNull();
      typeof(Errors).Should().NotBeNull();

      var services = new ServiceCollection();
      services.AddSingleton(new TranslationToolsClient(
         new HttpClient(new EmptySuccessHandler()),
         Options.Create(new TranslationToolsClientOptions { ApiKey = "api-key" }),
         new LocalTranslationToolsClientCache()
      ));
      services.AddSingleton<ITranslationToolsClient>(provider => provider.GetRequiredService<TranslationToolsClient>());
      using var provider = services.BuildServiceProvider();

      var runtimeClient = provider.GetRequiredService<TranslationToolsClient>();
      await runtimeClient.ApplyLocaleUpdateAsync(
         new System.Globalization.CultureInfo("en"),
         new Dictionary<TranslationRef, string?>
         {
            [new("/Localizations.resx", "Button.Save")] = "Cached save",
            [new("/Resources/Shared/Errors.resx", "404.title")] = "Cached not found"
         },
         TestContext.Current.CancellationToken
      );

      var previousCulture = CultureInfo.CurrentUICulture;

      try
      {
         CultureInfo.CurrentUICulture = new CultureInfo("en");
         Translations.SetServiceProvider(provider);

         Localizations.Keys.Button_Save.Origin.Should().Be("/Localizations.resx");
         Localizations.Keys.Button_Save.Key.Should().Be("Button.Save");
         Localizations.Keys.Action_Save.Origin.Should().Be("/Localizations.resx");
         Localizations.Keys.Action_Save.Key.Should().Be("Action-Save");

         Errors.Keys._404_title.Origin.Should().Be("/Resources/Shared/Errors.resx");
         Errors.Keys._404_title.Key.Should().Be("404.title");
         Errors.Keys.Status_Code.Origin.Should().Be("/Resources/Shared/Errors.resx");
         Errors.Keys.Status_Code.Key.Should().Be("Status.Code");

         Localizations.Button_Save.Should().Be("Cached save");
         Errors._404_title.Should().Be("Cached not found");

         var result = await FixtureUsage.ExerciseAsync(provider);

         result.SyncValue.Should().Be("Cached save");
         result.AsyncValue.Should().Be("Cached not found");

         var saveKey = result.SaveKey;
         saveKey.Origin.Should().Be("/Localizations.resx");
         saveKey.Key.Should().Be("Button.Save");

         var errorKey = result.ErrorKey;
         errorKey.Origin.Should().Be("/Resources/Shared/Errors.resx");
         errorKey.Key.Should().Be("404.title");
      }
      finally
      {
         CultureInfo.CurrentUICulture = previousCulture;
      }
   }

   private sealed class EmptySuccessHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
         {
            Content = new StringContent("[]")
         });
      }
   }
}
