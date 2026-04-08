using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Service registration helpers for the TranslationTools client.
/// </summary>
[PublicAPI]
public static class DependencyInjectionExtensions
{
   private const string HTTP_CLIENT_NAME = nameof(TranslationToolsClient);

   /// <summary>
   /// Register the TranslationTools client and configure its options.
   /// </summary>
   public static IServiceCollection AddTranslationToolsClient(this IServiceCollection services, Action<TranslationToolsClientOptions> options)
   {
      services.Configure(options);

      services.AddOptions<TranslationToolsClientOptions>()
         .PostConfigure<IOptions<RequestLocalizationOptions>>(static (clientOptions, localizationOptions) =>
            {
               if (clientOptions.SupportedLocales.Length > 0)
                  return;

               clientOptions.SupportedLocales = localizationOptions.Value.SupportedUICultures?.ToArray() ?? localizationOptions.Value.SupportedCultures?.ToArray() ?? [];
            }
         );

      services.AddHttpClient(HTTP_CLIENT_NAME);
      services.TryAddSingleton<TranslationToolsClient>(static serviceProvider =>
      {
         var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
         var httpClient = httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
         var clientOptions = serviceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>();
         var client = new TranslationToolsClient(httpClient, clientOptions);
         Translations.SetClient(client);
         return client;
      });
      services.TryAddSingleton<ITranslationToolsClient>(provider => provider.GetRequiredService<TranslationToolsClient>());
      services.TryAddSingleton<TranslationToolsLiveUpdateService>();

      return services;
   }

   /// <summary>
   ///   Initializes the Translation Tools Client.
   /// </summary>
   public static async Task InitializeTranslationToolsClientAsync(this WebApplication app, CancellationToken cancellationToken = default)
   {
      using var scope = app.Services.CreateScope();

      try
      {

         var client = scope.ServiceProvider.GetRequiredService<ITranslationToolsClient>();

         await client.Initialize(cancellationToken);

         var options = scope.ServiceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;
         if (options.EnableLiveUpdates)
         {
            var liveUpdateService = app.Services.GetRequiredService<TranslationToolsLiveUpdateService>();
            await liveUpdateService.StartAsync(app.Lifetime.ApplicationStopping);
         }
      }
      catch (Exception e)
      {
         var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
         var logger = loggerFactory.CreateLogger<ITranslationToolsClient>();
         logger.LogError(e, "Error initializing translation tools client");
      }
   }
}
