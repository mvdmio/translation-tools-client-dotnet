using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Migrate;
using mvdmio.TranslationTools.Tool.Pull;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class PushHandler
{
   private readonly ITranslationApiService _translationApiService;
   private readonly ResxMigrationScanner _scanner;
   private readonly ProjectTranslationStateBuilder _stateBuilder;
   private readonly IPushReporter _reporter;

   public PushHandler()
      : this(new TranslationApiService(), new ResxMigrationScanner(), new ProjectTranslationStateBuilder(), new ConsolePushReporter())
   {
   }

   internal PushHandler(ITranslationApiService translationApiService, ResxMigrationScanner scanner, ProjectTranslationStateBuilder stateBuilder, IPushReporter reporter)
   {
      _translationApiService = translationApiService;
      _scanner = scanner;
      _stateBuilder = stateBuilder;
      _reporter = reporter;
   }

   public Task HandleAsync(CancellationToken cancellationToken = default)
   {
      var config = ToolConfigurationLoader.Load();
      return HandleAsync(config, cancellationToken);
   }

   internal async Task HandleAsync(ToolConfiguration config, CancellationToken cancellationToken = default)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
      {
         _reporter.WriteError("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
         return;
      }

      if (string.IsNullOrWhiteSpace(config.DefaultLocale))
      {
         _reporter.WriteError("Error: No default locale provided. Add defaultLocale to .mvdmio-translations.yml.");
         return;
      }

      var projectDirectory = ResolveProjectDirectory(config);
      var scanResult = _scanner.ScanProject(projectDirectory);
      var (state, report) = _stateBuilder.Build(scanResult, config.DefaultLocale);

      _reporter.WriteInfo($"Scanning .resx files in {projectDirectory}...");
      _reporter.WriteInfo($"Pushing {report.KeyCount} translation keys across {report.Locales.Count} locales to {ToolConfiguration.DEFAULT_BASE_URL}...");

      var result = await _translationApiService.ImportProjectStateAsync(
         config.ApiKey,
         new ProjectTranslationStateImportRequest
         {
            DefaultLocale = state.DefaultLocale,
            Locales = [.. state.Locales],
            Items = [.. state.Items.Select(static item => new ProjectTranslationStateImportItemRequest {
               Key = item.Key,
               Translations = new Dictionary<string, string?>(item.Translations, StringComparer.Ordinal)
            })]
         },
         cancellationToken
      );

      _reporter.WriteInfo($"Push complete. Received {result.ReceivedKeyCount} keys across {result.ReceivedLocaleCount} locales.");
      _reporter.WriteInfo($"Created translations: {result.CreatedTranslationCount}. Updated translations: {result.UpdatedTranslationCount}.");
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
