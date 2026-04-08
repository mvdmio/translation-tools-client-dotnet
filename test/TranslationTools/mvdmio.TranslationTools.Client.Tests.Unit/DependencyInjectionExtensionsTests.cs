using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class DependencyInjectionExtensionsTests
{
   [Fact]
   public void AddTranslationToolsClient_ShouldPopulateSupportedLocalesFromRequestLocalizationOptions()
   {
      var services = new ServiceCollection();
      services.AddOptions();
      services.Configure<RequestLocalizationOptions>(options =>
      {
         options.SupportedUICultures = [new CultureInfo("en"), new CultureInfo("nl")];
      });

      services.AddTranslationToolsClient(options =>
      {
         options.ApiKey = "api-key";
      });

      using var provider = services.BuildServiceProvider();

      var options = provider.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;

      options.SupportedLocales.Select(static x => x.Name).Should().Equal("en", "nl");
      provider.GetRequiredService<ITranslationToolsClient>().Should().BeOfType<TranslationToolsClient>();
   }

   [Fact]
   public async Task InitializeTranslationToolsClientAsync_ShouldWorkWithStaticTranslationsWhenClientIsConfiguredInTestSetup()
   {
      var builder = WebApplication.CreateBuilder();
      builder.Services.AddSingleton<ITranslationToolsClient>(new StubTranslationToolsClient());

      await using var app = builder.Build();
      Translations.SetClient(app.Services.GetRequiredService<ITranslationToolsClient>());

      await app.InitializeTranslationToolsClientAsync(TestContext.Current.CancellationToken);

      var value = await Translations.GetAsync(new TranslationRef("/Localizations.resx", "Button.Save"), cancellationToken: TestContext.Current.CancellationToken);

      value.Should().Be("translated:Button.Save");
   }

   private sealed class StubTranslationToolsClient : ITranslationToolsClient
   {
      public Task Initialize(CancellationToken cancellationToken = default)
      {
         return Task.CompletedTask;
      }

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CancellationToken cancellationToken = default)
      {
         return GetAsync(translation, CultureInfo.CurrentUICulture, cancellationToken);
      }

      public Task<TranslationItemResponse> GetAsync(TranslationRef translation, CultureInfo locale, CancellationToken cancellationToken = default)
      {
         return Task.FromResult(new TranslationItemResponse
         {
            Origin = translation.Origin,
            Key = translation.Key,
            Value = $"translated:{translation.Key}"
         });
      }

      public Task<TranslationLocaleSnapshot> GetLocaleAsync(CultureInfo locale, CancellationToken cancellationToken = default)
      {
         return Task.FromResult(TranslationLocaleSnapshot.FromItems(locale.Name, []));
      }
   }
}
