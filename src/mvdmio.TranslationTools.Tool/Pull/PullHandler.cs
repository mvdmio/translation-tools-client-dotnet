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
   private readonly IPullFileSystem _fileSystem;
   private readonly IPullReporter _reporter;

   public PullHandler()
      : this(new TranslationApiService(), new ManifestFileBuilder(), new ManifestFileParser(), new ManifestFileMerger(new ManifestFileParser(), new ManifestFileBuilder()), new PullFileSystem(), new ConsolePullReporter())
   {
   }

   internal PullHandler(
      TranslationApiService translationApiService,
      ManifestFileBuilder manifestFileBuilder,
      ManifestFileParser manifestFileParser,
      ManifestFileMerger manifestFileMerger,
      IPullFileSystem fileSystem,
      IPullReporter reporter
   )
   {
      _translationApiService = translationApiService;
      _manifestFileBuilder = manifestFileBuilder;
      _manifestFileParser = manifestFileParser;
      _manifestFileMerger = manifestFileMerger;
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

      _reporter.WriteInfo($"Pulling default locale '{defaultLocale}' from {ToolConfiguration.DEFAULT_BASE_URL}...");
      var items = await _translationApiService.FetchLocaleAsync(request.ApiKey, defaultLocale, cancellationToken);

       var definitions = BuildPropertyDefinitions(items, request.KeyNaming, request.SharedKeyPrefix);
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

      _reporter.WriteInfo($"Wrote manifest to {request.OutputPath}");
      _reporter.WriteInfo($"Generated {definitions.Count} translation properties from default locale '{defaultLocale}'");
      if (!overwrite)
         _reporter.WriteInfo("Existing manifest values were preserved where matching properties already existed.");
   }

   internal static TranslationPullRequest? ResolveRequest(ToolConfiguration config)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
         return null;

      var outputPath = ToolPathResolver.GetOutputPath(config);
      var resolvedNamespace = string.IsNullOrWhiteSpace(config.Namespace) ? NamespaceResolver.Resolve(outputPath) : config.Namespace;

       return new TranslationPullRequest {
          ApiKey = config.ApiKey,
          OutputPath = outputPath,
          Namespace = resolvedNamespace,
          ClassName = config.ClassName,
          KeyNaming = config.KeyNaming,
          SharedKeyPrefix = config.SharedKeyPrefix
       };
    }

    internal static IReadOnlyCollection<ManifestPropertyDefinition> BuildPropertyDefinitions(IEnumerable<TranslationItemResponse> items, TranslationKeyNaming keyNaming, string? sharedKeyPrefix = null)
    {
       var definitions = new Dictionary<string, ManifestPropertyDefinition>(StringComparer.Ordinal);
       var normalizedPrefix = NormalizeSharedKeyPrefix(sharedKeyPrefix);

       foreach (var item in items.OrderBy(static x => x.Key, StringComparer.Ordinal))
       {
          var propertyKey = RemoveSharedKeyPrefix(item.Key, normalizedPrefix);
          var propertyName = ManifestPropertyNameResolver.Resolve(propertyKey);
          var derivedKey = TranslationKeyNamingConverter.Convert(propertyName, (int)keyNaming);
          var definition = new ManifestPropertyDefinition {
             PropertyName = propertyName,
            Key = item.Key,
            EmitExplicitKey = !string.Equals(derivedKey, item.Key, StringComparison.Ordinal),
            DefaultValue = item.Value
         };

         if (!definitions.TryAdd(propertyName, definition))
            definitions[propertyName] = MergeDuplicatePropertyDefinition(definitions[propertyName], definition, derivedKey, keyNaming);
      }

       return [.. definitions.Values];
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
