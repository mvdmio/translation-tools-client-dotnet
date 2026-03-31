using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;

namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class MigrateHandler
{
   private readonly IMigrateConfigurationLoader _configurationLoader;
   private readonly ITranslationApiService _translationApiService;
   private readonly ResxMigrationScanner _scanner;
   private readonly ProjectTranslationStateBuilder _stateBuilder;
   private readonly IMigratePullRunner _pullRunner;
   private readonly IMigrateReporter _reporter;

   public MigrateHandler()
      : this(new ToolConfigurationMigrateConfigurationLoader(), new TranslationApiService(), new ResxMigrationScanner(), new ProjectTranslationStateBuilder(), new PullRunner(), new ConsoleMigrateReporter())
   {
   }

   internal MigrateHandler(
      IMigrateConfigurationLoader configurationLoader,
      ITranslationApiService translationApiService,
      ResxMigrationScanner scanner,
      ProjectTranslationStateBuilder stateBuilder,
      IMigratePullRunner pullRunner,
      IMigrateReporter reporter
   )
   {
      _configurationLoader = configurationLoader;
      _translationApiService = translationApiService;
      _scanner = scanner;
      _stateBuilder = stateBuilder;
      _pullRunner = pullRunner;
      _reporter = reporter;
   }

   public Task HandleAsync(CancellationToken cancellationToken = default)
   {
      if (!_configurationLoader.TryLoad(out var config))
      {
         _reporter.WriteError("Error: Configuration file not found. Run 'translations init' first.");
         return Task.CompletedTask;
      }

      return HandleAsync(config, cancellationToken);
   }

   internal async Task HandleAsync(ToolConfiguration config, CancellationToken cancellationToken = default)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
      {
         _reporter.WriteError("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
         return;
      }

      var projectDirectory = Push.PushHandler.ResolveProjectDirectory(config);
      var metadata = await _translationApiService.FetchProjectMetadataAsync(config.ApiKey, cancellationToken);
      var scanResult = _scanner.ScanProject(projectDirectory);
      var resolvedDefaultLocale = !string.IsNullOrWhiteSpace(config.DefaultLocale)
         ? config.DefaultLocale
         : metadata.DefaultLocale;

      var (state, report) = _stateBuilder.Build(scanResult, resolvedDefaultLocale);

      _reporter.WriteInfo($"Scanning .resx files in {projectDirectory}...");
      _reporter.WriteInfo($"Detected {report.SourceFiles.Count} source files.");
      _reporter.WriteInfo($"Resolved default locale: {report.DefaultLocale}.");
      _reporter.WriteInfo($"Resolved locales: {string.Join(", ", report.Locales)}.");
      _reporter.WriteInfo($"Resolved {report.KeyCount} translation keys.");

      foreach (var localeCount in report.LocaleValueCounts.OrderBy(static x => x.Key, StringComparer.Ordinal))
         _reporter.WriteInfo($"Locale '{localeCount.Key}' non-empty value count: {localeCount.Value}.");

      if (state.Items.Count == 0)
         _reporter.WriteWarning("All discovered .resx files were empty. Importing locale metadata with an empty translation payload.");

      foreach (var warning in report.Warnings)
         _reporter.WriteWarning(warning);

      var response = await _translationApiService.ImportProjectStateAsync(
         config.ApiKey,
         new ProjectTranslationStateImportRequest {
            DefaultLocale = state.DefaultLocale,
            Locales = state.Locales.ToArray(),
            Items = state.Items.Select(
               static item => new ProjectTranslationStateImportItemRequest {
                  Key = item.Key,
                  Translations = item.Translations
               }
            ).ToArray()
         },
         cancellationToken
      );

      _reporter.WriteInfo($"Import complete. Keys: {response.ReceivedKeyCount}. Locales: {response.ReceivedLocaleCount}. Created translations: {response.CreatedTranslationCount}. Updated translations: {response.UpdatedTranslationCount}.");

      await _pullRunner.RunAsync(config, overwrite: true, cancellationToken);
      _reporter.WriteInfo("Manifest regenerated from API state.");
   }
}

internal interface IMigrateConfigurationLoader
{
   bool TryLoad(out ToolConfiguration config);
}

internal interface IMigratePullRunner
{
   Task RunAsync(ToolConfiguration config, bool overwrite, CancellationToken cancellationToken);
}

internal sealed class ToolConfigurationMigrateConfigurationLoader : IMigrateConfigurationLoader
{
   public bool TryLoad(out ToolConfiguration config)
   {
      return ToolConfigurationLoader.TryLoad(out config);
   }
}

internal interface IMigrateReporter
{
   void WriteInfo(string message);
   void WriteWarning(string message);
   void WriteError(string message);
}

internal sealed class PullRunner : IMigratePullRunner
{
   private readonly PullHandler _pullHandler = new();

   public Task RunAsync(ToolConfiguration config, bool overwrite, CancellationToken cancellationToken)
   {
      return _pullHandler.HandleAsync(config, overwrite, cancellationToken);
   }
}

internal sealed class ConsoleMigrateReporter : IMigrateReporter
{
   public void WriteInfo(string message)
   {
      Console.WriteLine(message);
   }

   public void WriteWarning(string message)
   {
      Console.WriteLine($"Warning: {message}");
   }

   public void WriteError(string message)
   {
      Console.Error.WriteLine(message);
   }
}
