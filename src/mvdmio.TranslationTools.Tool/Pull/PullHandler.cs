using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Scaffolding;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class PullHandler
{
   private readonly TranslationApiService _translationApiService;
   private readonly ManifestFileBuilder _manifestFileBuilder;
   private readonly ManifestFileParser _manifestFileParser;
   private readonly ManifestFileMerger _manifestFileMerger;
   private readonly TranslationSnapshotFileWriter _snapshotFileWriter;
   private readonly IPullFileSystem _fileSystem;
   private readonly IPullReporter _reporter;

   public PullHandler()
      : this(new TranslationApiService(), new ManifestFileBuilder(), new ManifestFileParser(), new ManifestFileMerger(new ManifestFileParser(), new ManifestFileBuilder()), new TranslationSnapshotFileWriter(), new PullFileSystem(), new ConsolePullReporter())
   {
   }

   internal PullHandler(
      TranslationApiService translationApiService,
      ManifestFileBuilder manifestFileBuilder,
      ManifestFileParser manifestFileParser,
      ManifestFileMerger manifestFileMerger,
      TranslationSnapshotFileWriter snapshotFileWriter,
      IPullFileSystem fileSystem,
      IPullReporter reporter
    )
   {
      _translationApiService = translationApiService;
      _manifestFileBuilder = manifestFileBuilder;
      _manifestFileParser = manifestFileParser;
      _manifestFileMerger = manifestFileMerger;
      _snapshotFileWriter = snapshotFileWriter;
      _fileSystem = fileSystem;
      _reporter = reporter;
   }

   public Task HandleAsync(bool overwrite, CancellationToken cancellationToken = default)
   {
      var config = ToolConfigurationLoader.Load();
      return HandleAsync(config, overwrite, cancellationToken);
   }

   internal async Task HandleAsync(ToolConfiguration config, bool overwrite, CancellationToken cancellationToken = default)
   {
      var request = ResolveRequest(config);
      if (request is null)
      {
         _reporter.WriteError("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
         return;
      }

      _reporter.WriteInfo($"Retrieving project metadata from {ToolConfiguration.DEFAULT_BASE_URL}...");
      var metadata = await _translationApiService.FetchProjectMetadataAsync(request.ApiKey, cancellationToken);
      var locales = metadata.Locales
         .Where(static locale => !string.IsNullOrWhiteSpace(locale))
         .Distinct(StringComparer.Ordinal)
         .ToArray();

      if (locales.Length == 0)
         throw new InvalidOperationException("Project has no locales configured.");

      var defaultLocale = !string.IsNullOrWhiteSpace(metadata.DefaultLocale)
         ? metadata.DefaultLocale!
         : locales[0];

      var localeItems = new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal);

      foreach (var locale in locales.OrderBy(static x => x, StringComparer.Ordinal))
      {
         _reporter.WriteInfo($"Pulling locale '{locale}' from {ToolConfiguration.DEFAULT_BASE_URL}...");
         localeItems[locale] = await _translationApiService.FetchLocaleAsync(request.ApiKey, locale, cancellationToken);
      }

      var defaultLocaleItems = localeItems[defaultLocale];
      var definitions = BuildPropertyDefinitions(localeItems.Values.SelectMany(static x => x), defaultLocaleItems, request.KeyNaming, request.SharedKeyPrefix);
      string manifest;

      if (!overwrite && _fileSystem.FileExists(request.OutputPath))
      {
         var existingContent = await _fileSystem.ReadAllTextAsync(request.OutputPath, cancellationToken);
         var mergeResult = _manifestFileMerger.Merge(existingContent, request.ClassName, request.KeyNaming, definitions);

         if (mergeResult.ClassDeclaration is not null)
         {
            manifest = mergeResult.Content;
            definitions = _manifestFileParser.Parse(manifest, request.ClassName, request.KeyNaming);
         }
         else
         {
            var existingDefinitions = _manifestFileParser.Parse(existingContent, request.ClassName, request.KeyNaming);
            definitions = MergePropertyDefinitions(existingDefinitions, definitions);
            manifest = _manifestFileBuilder.Build(
               new ManifestGenerationOptions {
                  Namespace = request.Namespace,
                  ClassName = request.ClassName,
                  KeyNaming = request.KeyNaming
               },
               definitions
            );
         }
      }
      else
      {
         manifest = _manifestFileBuilder.Build(
            new ManifestGenerationOptions {
               Namespace = request.Namespace,
               ClassName = request.ClassName,
               KeyNaming = request.KeyNaming
            },
            definitions
         );
      }

      var outputDirectory = Path.GetDirectoryName(request.OutputPath);
      if (!string.IsNullOrWhiteSpace(outputDirectory))
         _fileSystem.CreateDirectory(outputDirectory);

      await _fileSystem.WriteAllTextAsync(request.OutputPath, manifest, cancellationToken);

      var snapshot = BuildSnapshot(defaultLocale, locales, localeItems);
      await _fileSystem.WriteAllTextAsync(request.SnapshotPath, _snapshotFileWriter.Write(snapshot), cancellationToken);

      _reporter.WriteInfo($"Wrote manifest to {request.OutputPath}");
      _reporter.WriteInfo($"Wrote snapshot to {request.SnapshotPath}");
      _reporter.WriteInfo($"Generated {definitions.Count} translation properties from {locales.Length} locales.");
      if (!overwrite)
         _reporter.WriteInfo("Existing manifest values were preserved where matching properties already existed.");
   }

   internal static TranslationPullRequest? ResolveRequest(ToolConfiguration config)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
         return null;

      var projectContext = ToolProjectResolver.Resolve(config);
      var outputPath = ToolPathResolver.GetOutputPath(config, projectContext);
      var resolvedNamespace = string.IsNullOrWhiteSpace(config.Namespace) ? NamespaceResolver.Resolve(outputPath, projectContext) : config.Namespace;

      return new TranslationPullRequest {
         ApiKey = config.ApiKey,
         ProjectDirectory = projectContext.ProjectDirectory,
         OutputPath = outputPath,
         SnapshotPath = ToolProjectResolver.GetSnapshotPath(config),
         Namespace = resolvedNamespace,
         ClassName = config.ClassName,
         KeyNaming = config.KeyNaming,
         SharedKeyPrefix = config.SharedKeyPrefix
      };
   }

   internal static IReadOnlyCollection<ManifestPropertyDefinition> BuildPropertyDefinitions(
      IEnumerable<TranslationItemResponse> items,
      IEnumerable<TranslationItemResponse> defaultLocaleItems,
      TranslationKeyNaming keyNaming,
      string? sharedKeyPrefix = null
   )
   {
      var definitions = new Dictionary<string, ManifestPropertyDefinition>(StringComparer.Ordinal);
      var normalizedPrefix = NormalizeSharedKeyPrefix(sharedKeyPrefix);
      var defaultValues = defaultLocaleItems.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.Ordinal);

      foreach (var key in items.Select(static x => x.Key).Distinct(StringComparer.Ordinal).OrderBy(static x => x, StringComparer.Ordinal))
      {
         var propertyKey = RemoveSharedKeyPrefix(key, normalizedPrefix);
         var propertyName = ManifestPropertyNameResolver.Resolve(propertyKey);
         var derivedKey = TranslationKeyNamingConverter.Convert(propertyName, (int)keyNaming);
         var definition = new ManifestPropertyDefinition {
            PropertyName = propertyName,
            Key = key,
            EmitExplicitKey = !string.Equals(derivedKey, key, StringComparison.Ordinal),
            DefaultValue = defaultValues.GetValueOrDefault(key)
         };

         if (!definitions.TryAdd(propertyName, definition))
            definitions[propertyName] = MergeDuplicatePropertyDefinition(definitions[propertyName], definition, derivedKey, keyNaming);
      }

      return [.. definitions.Values];
   }

   private static TranslationSnapshotFile BuildSnapshot(
      string defaultLocale,
      IEnumerable<string> locales,
      IReadOnlyDictionary<string, TranslationItemResponse[]> localeItems
   )
   {
      return new TranslationSnapshotFile {
         SchemaVersion = 1,
         Project = new TranslationSnapshotProject {
            DefaultLocale = defaultLocale,
            Locales = locales.OrderBy(static x => x, StringComparer.Ordinal).ToArray()
         },
         Translations = localeItems
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
               static x => x.Key,
               static x => (IReadOnlyDictionary<string, string?>)x.Value
                  .OrderBy(static item => item.Key, StringComparer.Ordinal)
                  .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal),
               StringComparer.Ordinal
            )
      };
   }

   private static string? NormalizeSharedKeyPrefix(string? sharedKeyPrefix)
   {
      if (string.IsNullOrWhiteSpace(sharedKeyPrefix))
         return null;

      return sharedKeyPrefix.TrimEnd('.');
   }

   private static string RemoveSharedKeyPrefix(string key, string? sharedKeyPrefix)
   {
      if (string.IsNullOrWhiteSpace(sharedKeyPrefix))
         return key;

      if (!key.StartsWith(sharedKeyPrefix, StringComparison.Ordinal))
         return key;

      if (key.Length == sharedKeyPrefix.Length)
         return key;

      return key[sharedKeyPrefix.Length] == '.'
         ? key[(sharedKeyPrefix.Length + 1)..]
         : key;
   }

   private static ManifestPropertyDefinition MergeDuplicatePropertyDefinition(
      ManifestPropertyDefinition existingDefinition,
      ManifestPropertyDefinition incomingDefinition,
      string derivedKey,
      TranslationKeyNaming keyNaming
   )
   {
      if (string.Equals(existingDefinition.Key, incomingDefinition.Key, StringComparison.Ordinal))
         throw new InvalidOperationException($"Key '{incomingDefinition.Key}' resolves to duplicate property '{incomingDefinition.PropertyName}'. Existing key: '{existingDefinition.Key}'.");

      var existingMatchesDerivedKey = string.Equals(existingDefinition.Key, derivedKey, StringComparison.Ordinal);
      var incomingMatchesDerivedKey = string.Equals(incomingDefinition.Key, derivedKey, StringComparison.Ordinal);

      if (existingMatchesDerivedKey == incomingMatchesDerivedKey)
         throw new InvalidOperationException($"Keys '{existingDefinition.Key}' and '{incomingDefinition.Key}' both resolve to property '{incomingDefinition.PropertyName}' and cannot be disambiguated for keyNaming '{keyNaming}'.");

      var preferredDefinition = incomingMatchesDerivedKey ? incomingDefinition : existingDefinition;
      var fallbackDefinition = incomingMatchesDerivedKey ? existingDefinition : incomingDefinition;

      return new ManifestPropertyDefinition {
         PropertyName = preferredDefinition.PropertyName,
         Key = preferredDefinition.Key,
         EmitExplicitKey = preferredDefinition.EmitExplicitKey,
         DefaultValue = string.IsNullOrWhiteSpace(preferredDefinition.DefaultValue) ? fallbackDefinition.DefaultValue : preferredDefinition.DefaultValue
      };
   }

   internal static IReadOnlyCollection<ManifestPropertyDefinition> MergePropertyDefinitions(
      IReadOnlyCollection<ManifestPropertyDefinition> existingDefinitions,
      IReadOnlyCollection<ManifestPropertyDefinition> incomingDefinitions
   )
   {
      var merged = new List<ManifestPropertyDefinition>(existingDefinitions);
      var knownKeys = existingDefinitions.ToDictionary(static x => x.Key, static x => x, StringComparer.Ordinal);

      foreach (var incomingDefinition in incomingDefinitions)
      {
         if (knownKeys.ContainsKey(incomingDefinition.Key))
            continue;

         merged.Add(incomingDefinition);
         knownKeys[incomingDefinition.Key] = incomingDefinition;
      }

      return merged;
   }
}

internal interface IPullReporter
{
   void WriteInfo(string message);
   void WriteError(string message);
}

internal sealed class ConsolePullReporter : IPullReporter
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

internal interface IPullFileSystem
{
   void CreateDirectory(string path);
   bool FileExists(string path);
   Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
   Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken);
}

internal sealed class PullFileSystem : IPullFileSystem
{
   public void CreateDirectory(string path)
   {
      Directory.CreateDirectory(path);
   }

   public bool FileExists(string path)
   {
      return File.Exists(path);
   }

   public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
   {
      return File.ReadAllTextAsync(path, cancellationToken);
   }

   public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
   {
      return File.WriteAllTextAsync(path, contents, cancellationToken);
   }
}
