using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class PushHandler
{
   private readonly ITranslationApiService _translationApiService;
   private readonly ProjectManifestScanner _projectManifestScanner;
   private readonly IPushReporter _reporter;

   public PushHandler()
      : this(new TranslationApiService(), new ProjectManifestScanner(), new ConsolePushReporter())
   {
   }

   internal PushHandler(ITranslationApiService translationApiService, ProjectManifestScanner projectManifestScanner, IPushReporter reporter)
   {
      _translationApiService = translationApiService;
      _projectManifestScanner = projectManifestScanner;
      _reporter = reporter;
   }

   public Task HandleAsync(bool prune, CancellationToken cancellationToken = default)
   {
      var config = ToolConfigurationLoader.Load();
      return HandleAsync(config, prune, cancellationToken);
   }

   internal async Task HandleAsync(ToolConfiguration config, bool prune, CancellationToken cancellationToken = default)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
      {
         _reporter.WriteError("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
         return;
      }

      var projectContext = ToolProjectResolver.Resolve(config);
      var projectDirectory = projectContext.ProjectDirectory;
      if (string.IsNullOrWhiteSpace(config.DefaultLocale))
         throw new InvalidOperationException("defaultLocale is required in .mvdmio-translations.yml.");

      var scanResult = _projectManifestScanner.ScanProject(projectContext.ProjectName, projectDirectory, config.DefaultLocale);

      _reporter.WriteInfo($"Scanning .resx translations in {projectDirectory}...");
      _reporter.WriteInfo($"Pushing {scanResult.Items.Count} translation values to {ToolConfiguration.DEFAULT_BASE_URL}...");

      var result = await _translationApiService.PushProjectTranslationsAsync(
         config.ApiKey,
         new TranslationPushRequest
         {
            Items = scanResult.Items.Select(
               static x => new TranslationPushItemRequest
               {
                  Origin = x.Origin,
                  Locale = x.Locale,
                  Key = x.Key,
                  Value = x.Value
               }
            ).ToArray()
         },
         cancellationToken
      );

      _reporter.WriteInfo($"Push complete. Synced {result.ReceivedKeyCount} translation values.");
      _reporter.WriteInfo($"Created: {result.CreatedKeyCount}. Updated values: {result.UpdatedKeyCount}. Removed: {result.RemovedKeyCount}.");
   }

   internal static string ResolveProjectDirectory(ToolConfiguration config)
   {
      return ToolProjectResolver.Resolve(config).ProjectDirectory;
   }
}

internal interface IPushReporter
{
   void WriteInfo(string message);
   void WriteError(string message);
}

internal sealed class ConsolePushReporter : IPushReporter
{
   public void WriteInfo(string message)
   {
      Console.WriteLine(message);
   }

   public void WriteError(string message)
   {
      Console.Error.WriteLine(message);
   }
}
