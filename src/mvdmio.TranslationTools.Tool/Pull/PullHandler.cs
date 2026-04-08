using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Scaffolding;
using System.Text;
using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class PullHandler
{
   private readonly ITranslationApiService _translationApiService;
   private readonly TranslationSnapshotFileWriter _snapshotFileWriter;
   private readonly IPullFileSystem _fileSystem;
   private readonly IPullReporter _reporter;

   public PullHandler()
      : this(new TranslationApiService(), new TranslationSnapshotFileWriter(), new PullFileSystem(), new ConsolePullReporter())
   {
   }

   internal PullHandler(
      ITranslationApiService translationApiService,
      TranslationSnapshotFileWriter snapshotFileWriter,
      IPullFileSystem fileSystem,
      IPullReporter reporter
   )
   {
      _translationApiService = translationApiService;
      _snapshotFileWriter = snapshotFileWriter;
      _fileSystem = fileSystem;
      _reporter = reporter;
   }

   public Task HandleAsync(bool prune, CancellationToken cancellationToken = default)
   {
      var config = ToolConfigurationLoader.Load();
      return HandleAsync(config, prune, cancellationToken);
   }

   internal async Task HandleAsync(ToolConfiguration config, bool prune, CancellationToken cancellationToken = default)
   {
      var request = ResolveRequest(config);
      if (request is null)
      {
         _reporter.WriteError("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
         return;
      }

      if (string.IsNullOrWhiteSpace(config.DefaultLocale))
      {
         _reporter.WriteError("Error: No default locale provided. Add defaultLocale to .mvdmio-translations.yml.");
         return;
      }

      _reporter.WriteInfo($"Retrieving project metadata from {ToolConfiguration.DEFAULT_BASE_URL}...");
      var metadata = await _translationApiService.FetchProjectMetadataAsync(request.ApiKey, cancellationToken);
      var locales = metadata.Locales
         .Where(static locale => !string.IsNullOrWhiteSpace(locale))
         .Distinct(StringComparer.Ordinal)
         .OrderBy(static locale => locale, StringComparer.Ordinal)
         .ToArray();

      if (locales.Length == 0)
         throw new InvalidOperationException("Project has no locales configured.");

      var defaultLocale = !string.IsNullOrWhiteSpace(metadata.DefaultLocale)
         ? metadata.DefaultLocale!
         : config.DefaultLocale!;
      var localeItems = new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal);
      foreach (var locale in locales)
      {
         _reporter.WriteInfo($"Pulling locale '{locale}' from {ToolConfiguration.DEFAULT_BASE_URL}...");
         localeItems[locale] = await _translationApiService.FetchLocaleAsync(request.ApiKey, locale, cancellationToken);
      }

      var allItems = localeItems
         .SelectMany(static pair => pair.Value.Select(item => (Locale: pair.Key, Item: item)))
         .GroupBy(static x => (Origin: NormalizeOrigin(x.Item.Origin), Locale: x.Locale), static x => x.Item)
         .ToArray();

      foreach (var group in allItems)
      {
         var filePath = BuildFilePath(request.ProjectDirectory, group.Key.Origin, group.Key.Locale, defaultLocale);
         var fileDirectory = Path.GetDirectoryName(filePath);
         if (!string.IsNullOrWhiteSpace(fileDirectory))
            _fileSystem.CreateDirectory(fileDirectory);

         var content = BuildResx(group.OrderBy(static item => item.Key, StringComparer.Ordinal));
         await _fileSystem.WriteAllTextAsync(filePath, content, cancellationToken);
      }

      var snapshot = BuildSnapshot(defaultLocale, locales, localeItems);
      await _fileSystem.WriteAllTextAsync(request.SnapshotPath, _snapshotFileWriter.Write(snapshot), cancellationToken);

      _reporter.WriteInfo($"Wrote snapshot to {request.SnapshotPath}");
      _reporter.WriteInfo($"Updated {allItems.Length} .resx files from {locales.Length} locales.");

      if (prune)
         _reporter.WriteInfo("Prune requested. Remote-aligned file deletion not fully implemented yet.");
   }

   internal static TranslationPullRequest? ResolveRequest(ToolConfiguration config)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
         return null;

      var projectContext = ToolProjectResolver.Resolve(config);
      var outputPath = ToolPathResolver.GetOutputPath(config, projectContext);
      var outputDirectory = Path.GetDirectoryName(outputPath);
      var relativeOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
         ? string.Empty
         : Path.GetRelativePath(projectContext.ProjectDirectory, outputDirectory);
      var resolvedNamespace = !string.IsNullOrWhiteSpace(config.Namespace)
         ? config.Namespace
         : string.IsNullOrWhiteSpace(relativeOutputDirectory) || relativeOutputDirectory == "."
            ? projectContext.RootNamespace
            : projectContext.RootNamespace + "." + relativeOutputDirectory.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');

      return new TranslationPullRequest
      {
         ApiKey = config.ApiKey,
         ProjectDirectory = projectContext.ProjectDirectory,
         OutputPath = outputPath,
         SnapshotPath = ToolProjectResolver.GetSnapshotPath(config),
         Namespace = resolvedNamespace,
         ClassName = config.ClassName,
         SharedKeyPrefix = config.SharedKeyPrefix
      };
   }

   private static string BuildFilePath(string projectDirectory, string origin, string locale, string defaultLocale)
   {
      var normalizedOrigin = NormalizeOrigin(origin).TrimStart('/');
      var basePath = Path.Combine(projectDirectory, normalizedOrigin.Replace('/', Path.DirectorySeparatorChar));

      if (string.Equals(locale, defaultLocale, StringComparison.Ordinal))
         return basePath;

      var directory = Path.GetDirectoryName(basePath) ?? projectDirectory;
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
      return Path.Combine(directory, fileNameWithoutExtension + "." + locale + ".resx");
   }

   private static string BuildResx(IEnumerable<TranslationItemResponse> items)
   {
      var root = new XElement("root",
         items.Select(item => new XElement("data",
            new XAttribute("name", item.Key),
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            new XElement("value", item.Value ?? string.Empty)
         ))
      );

      var builder = new StringBuilder();
      builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
      builder.Append(root.ToString(SaveOptions.None));
      builder.AppendLine();
      return builder.ToString();
   }

   private static TranslationSnapshotFile BuildSnapshot(
      string defaultLocale,
      IEnumerable<string> locales,
      IReadOnlyDictionary<string, TranslationItemResponse[]> localeItems
   )
   {
      return new TranslationSnapshotFile
      {
         SchemaVersion = 1,
         Project = new TranslationSnapshotProject
         {
            DefaultLocale = defaultLocale,
            Locales = locales.OrderBy(static x => x, StringComparer.Ordinal).ToArray()
         },
         Translations = localeItems
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
               static x => x.Key,
               static x => (IReadOnlyCollection<TranslationSnapshotItemFile>)x.Value
                  .OrderBy(static item => item.Origin, StringComparer.OrdinalIgnoreCase)
                  .ThenBy(static item => item.Key, StringComparer.Ordinal)
                  .Select(static item => new TranslationSnapshotItemFile
                  {
                     Origin = item.Origin,
                     Key = item.Key,
                     Value = item.Value
                  })
                  .ToArray(),
               StringComparer.Ordinal
            )
      };
   }

   internal static IReadOnlyCollection<ManifestPropertyDefinition> BuildPropertyDefinitions(
      IEnumerable<TranslationItemResponse> items,
      IEnumerable<TranslationItemResponse> defaultLocaleItems,
      string? sharedKeyPrefix = null
   )
   {
      var defaultValues = defaultLocaleItems
         .GroupBy(static x => x.Key, StringComparer.Ordinal)
         .ToDictionary(static x => x.Key, static x => x.Last().Value, StringComparer.Ordinal);
      var duplicateKeys = items.GroupBy(static x => x.Key, StringComparer.Ordinal).Where(static x => x.Count() > 1).Select(static x => x.Key).ToArray();
      if (duplicateKeys.Length > 0)
         throw new ArgumentException($"Duplicate translation keys: {string.Join(", ", duplicateKeys)}", nameof(items));

      var definitions = new Dictionary<string, ManifestPropertyDefinition>(StringComparer.Ordinal);
      var canonicalByProperty = new Dictionary<string, TranslationItemResponse>(StringComparer.Ordinal);

      foreach (var item in items.OrderBy(static x => x.Key, StringComparer.Ordinal))
      {
         var effectiveKey = TrimSharedKeyPrefix(item.Key, sharedKeyPrefix);
         var propertyName = ManifestPropertyNameResolver.Resolve(effectiveKey);

         if (canonicalByProperty.TryGetValue(propertyName, out var existing))
         {
            var existingMatchesNaming = string.Equals(propertyName, TrimSharedKeyPrefix(existing.Key, sharedKeyPrefix), StringComparison.Ordinal);
            var currentMatchesNaming = string.Equals(propertyName, effectiveKey, StringComparison.Ordinal);

            if (!existingMatchesNaming && currentMatchesNaming)
               canonicalByProperty[propertyName] = item;

            continue;
         }

         canonicalByProperty[propertyName] = item;
      }

      foreach (var pair in canonicalByProperty.OrderBy(static x => x.Key, StringComparer.Ordinal))
      {
         var item = pair.Value;
         var effectiveKey = TrimSharedKeyPrefix(item.Key, sharedKeyPrefix);
         var propertyName = pair.Key;
         definitions[propertyName] = new ManifestPropertyDefinition
         {
            PropertyName = propertyName,
            Key = item.Key,
            EmitExplicitKey = !string.IsNullOrWhiteSpace(sharedKeyPrefix) || !string.Equals(propertyName, effectiveKey, StringComparison.Ordinal),
            DefaultValue = defaultValues.GetValueOrDefault(item.Key)
         };
      }

      return definitions.Values.ToArray();
   }

   private static string TrimSharedKeyPrefix(string key, string? sharedKeyPrefix)
   {
      if (string.IsNullOrWhiteSpace(sharedKeyPrefix))
         return key;

      var prefix = sharedKeyPrefix.Trim();
      if (!key.StartsWith(prefix + ".", StringComparison.Ordinal))
         return key;

      return key[(prefix.Length + 1)..];
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

   private static string NormalizeOrigin(string origin)
   {
      return origin.Trim().Replace('\\', '/');
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
   Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken);
}

internal sealed class PullFileSystem : IPullFileSystem
{
   public void CreateDirectory(string path)
   {
      Directory.CreateDirectory(path);
   }

   public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
   {
      return File.WriteAllTextAsync(path, contents, cancellationToken);
   }
}
