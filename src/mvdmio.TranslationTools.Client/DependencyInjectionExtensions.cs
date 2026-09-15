using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client.Internal;
using mvdmio.TranslationTools.Client.Placeholders;
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
      services.AddHttpContextAccessor();
      services.TryAddSingleton<IClientIdStore, FileClientIdStore>();
      services.TryAddSingleton(GlobalPlaceholderRegistry.Empty);
      services.TryAddSingleton<TranslationToolsClient>(static serviceProvider =>
      {
         var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
         var httpClient = httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
         var clientOptions = serviceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>();
         var clientIdStore = serviceProvider.GetRequiredService<IClientIdStore>();
         var timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
         var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<TranslationToolsClient>();
         var client = new TranslationToolsClient(httpClient, clientOptions, new LocalTranslationToolsClientCache(), timeProvider, clientIdStore, logger, TranslationCatalog.Entries);
         Translations.SetClient(client);
         return client;
      });
      services.TryAddSingleton<ITranslationToolsClient>(provider => provider.GetRequiredService<TranslationToolsClient>());
      services.TryAddSingleton<TranslationToolsLiveUpdateService>();

      return services;
   }

   /// <summary>
   /// Register a configuration type whose <see cref="GlobalPlaceholderAttribute"/> instance properties supply
   /// global placeholder values. The type is registered scoped and resolved per request through the ambient
   /// request scope. The declared global names are pushed to the server at startup.
   /// </summary>
   /// <typeparam name="TConfig">A type with <see cref="GlobalPlaceholderAttribute"/> instance properties.</typeparam>
   public static IServiceCollection AddTranslationToolsGlobalPlaceholders<TConfig>(this IServiceCollection services)
      where TConfig : class
   {
      services.TryAddScoped<TConfig>();

      var registry = GlobalPlaceholderRegistry.Build(typeof(TConfig));

      // Replace the default (Empty) registry registration with the built one.
      services.RemoveAll<GlobalPlaceholderRegistry>();
      services.AddSingleton(registry);

      return services;
   }

   /// <summary>
   /// Initializes the Translation Tools client: preloads supported locales, sends every missing local
   /// catalog key (Neutral value as the default locale plus sibling-locale values; existing snapshot
   /// keys are not overwritten), pushes declared global placeholder names, and starts live updates when
   /// enabled. Failures are logged and do not fail application startup.
   /// </summary>
   public static async Task InitializeTranslationToolsClientAsync(this WebApplication app, CancellationToken cancellationToken = default)
   {
      ConfigurePlaceholderRuntime(app.Services);

      using var scope = app.Services.CreateScope();

      try
      {

         var client = scope.ServiceProvider.GetRequiredService<ITranslationToolsClient>();

         await client.Initialize(cancellationToken);

         await PushGlobalsAsync(scope.ServiceProvider, client, cancellationToken);

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

   private static void ConfigurePlaceholderRuntime(IServiceProvider rootServices)
   {
      var registry = rootServices.GetService<GlobalPlaceholderRegistry>() ?? GlobalPlaceholderRegistry.Empty;
      var options = rootServices.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;
      var loggerFactory = rootServices.GetService<ILoggerFactory>();
      var logger = loggerFactory?.CreateLogger("mvdmio.TranslationTools.Client.Placeholders");

      // Resolve globals through the current request's RequestServices when available, else the root container.
      var httpContextAccessor = rootServices.GetService<IHttpContextAccessor>();
      AmbientScope.SetAccessor(() => httpContextAccessor?.HttpContext?.RequestServices ?? rootServices);

      var resolver = new GlobalPlaceholderResolver(registry);
      PlaceholderRuntime.Configure(resolver, options.ThrowOnPlaceholderError, logger);
   }

   private static async Task PushGlobalsAsync(IServiceProvider scopedServices, ITranslationToolsClient client, CancellationToken cancellationToken)
   {
      var registry = scopedServices.GetService<GlobalPlaceholderRegistry>();
      if (registry is null || registry.Names.Count == 0)
         return;

      if (client is not TranslationToolsClient concreteClient)
         return;

      await concreteClient.PushGlobalsAsync(registry.Names.ToArray(), cancellationToken);
   }
}
