using System.Globalization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
      provider.GetRequiredService<ITranslationToolsClient>().Should().BeOfType<Internal.TranslationToolsClientRuntime>();
   }
}
