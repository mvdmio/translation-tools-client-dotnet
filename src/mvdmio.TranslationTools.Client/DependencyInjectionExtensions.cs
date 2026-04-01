using JetBrains.Annotations;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

      services.AddOptions<TranslationToolsClientOptions>().PostConfigure<IOptions<RequestLocalizationOptions>>(
         static (clientOptions, localizationOptions) => {
            if (clientOptions.SupportedLocales.Length > 0)
               return;

            clientOptions.SupportedLocales = localizationOptions.Value.SupportedUICultures?.ToArray() ?? localizationOptions.Value.SupportedCultures?.ToArray() ?? [];
         }
      );

       services.AddHttpClient(HTTP_CLIENT_NAME);
        services.TryAddSingleton<ITranslationToolsClient>(static serviceProvider => {
           var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
           var httpClient = httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
           var clientOptions = serviceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>();
           return new TranslationToolsClient(httpClient, clientOptions, serviceProvider);
        });
       services.TryAddSingleton<TranslationToolsLiveUpdateService>();

      return services;
   }

   /// <summary>
   ///   Initializes the Translation Tools Client.
   /// </summary>
   public static async Task InitializeTranslationToolsClientAsync(this WebApplication app, CancellationToken cancellationToken = default)
   {
      using var scope = app.Services.CreateScope();
      var client = scope.ServiceProvider.GetRequiredService<ITranslationToolsClient>();
      var rootAssembly = Assembly.GetEntryAssembly() ?? app.GetType().Assembly;

      foreach (var assembly in GetManifestAssemblies(rootAssembly))
         TranslationManifestRuntime.RegisterClient(assembly, client);

      await client.Initialize(cancellationToken);

      var options = scope.ServiceProvider.GetRequiredService<IOptions<TranslationToolsClientOptions>>().Value;
      if (options.EnableLiveUpdates)
      {
         var liveUpdateService = app.Services.GetRequiredService<TranslationToolsLiveUpdateService>();
         await liveUpdateService.StartAsync(app.Lifetime.ApplicationStopping);
      }
   }

   private static Assembly[] GetManifestAssemblies(Assembly rootAssembly)
   {
      return AppDomain.CurrentDomain.GetAssemblies()
         .Where(static assembly => !assembly.IsDynamic)
         .Where(assembly => assembly == rootAssembly || ReferencesAssembly(assembly, rootAssembly))
         .Where(static assembly => GetLoadableTypes(assembly).Any(static type => type.GetCustomAttributes(typeof(TranslationsAttribute), inherit: false).Length > 0))
         .Distinct()
         .ToArray();
   }

   private static bool ReferencesAssembly(Assembly candidate, Assembly rootAssembly)
   {
      return candidate.GetReferencedAssemblies().Any(reference => string.Equals(reference.FullName, rootAssembly.FullName, StringComparison.Ordinal));
   }

   private static Type[] GetLoadableTypes(Assembly assembly)
   {
      try
      {
         return assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException exception)
      {
         return exception.Types.Where(static x => x is not null).Cast<Type>().ToArray();
      }
   }
}
