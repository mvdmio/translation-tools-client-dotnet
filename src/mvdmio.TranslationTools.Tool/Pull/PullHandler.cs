using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Migrate;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class PullHandler
{
   private readonly ITranslationApiService _translationApiService;
   private readonly ResxMigrationScanner _scanner;
   private readonly ResxFileParser _resxFileParser;
   private readonly ResxFileWriter _resxFileWriter;
   private readonly IPullFileSystem _fileSystem;
   private readonly IPullReporter _reporter;

   public PullHandler()
      : this(new TranslationApiService(), new ResxMigrationScanner(), new ResxFileParser(), new ResxFileWriter(), new PullFileSystem(), new ConsolePullReporter())
   {
   }

   internal PullHandler(
      ITranslationApiService translationApiService,
      ResxMigrationScanner scanner,
      ResxFileParser resxFileParser,
      ResxFileWriter resxFileWriter,
      IPullFileSystem fileSystem,
      IPullReporter reporter
   )
   {
      _translationApiService = translationApiService;
      _scanner = scanner;
      _resxFileParser = resxFileParser;
      _resxFileWriter = resxFileWriter;
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

      IReadOnlyCollection<ResxFileModel> files;
      try
      {
         files = BuildResxFiles(request.ProjectDirectory, defaultLocale, localeItems, prune);
      }
      catch (OperationCanceledException)
      {
         throw;
      }
      catch (Exception exception)
      {
         _reporter.WriteError(exception.Message);
         throw;
      }

      foreach (var file in files)
      {
         var directory = Path.GetDirectoryName(file.FilePath);
         if (!string.IsNullOrWhiteSpace(directory))
            _fileSystem.CreateDirectory(directory);

         try
         {
            await _fileSystem.WriteAllTextAsync(file.FilePath, _resxFileWriter.Write(file), cancellationToken);
         }
         catch (OperationCanceledException)
         {
            throw;
         }
         catch (Exception exception)
         {
            _reporter.WriteError($"Failed to write {Path.GetRelativePath(request.ProjectDirectory, file.FilePath)}. {FormatEntryDetails(file.Entries)} Error: {exception.Message}");
            throw;
         }

         _reporter.WriteInfo($"Wrote {Path.GetRelativePath(request.ProjectDirectory, file.FilePath)}");
      }

      if (prune)
      {
         foreach (var staleFile in FindStaleFiles(request.ProjectDirectory, files.Select(static x => x.FilePath)))
         {
            _fileSystem.DeleteFile(staleFile);
            _reporter.WriteInfo($"Removed {Path.GetRelativePath(request.ProjectDirectory, staleFile)}");
         }
      }
   }

   internal static TranslationPullRequest? ResolveRequest(ToolConfiguration config)
   {
      if (string.IsNullOrWhiteSpace(config.ApiKey))
         return null;

      var projectContext = ToolProjectResolver.Resolve(config);
      return new TranslationPullRequest
      {
         ApiKey = config.ApiKey,
         ProjectDirectory = projectContext.ProjectDirectory
      };
   }

   internal IReadOnlyCollection<ResxFileModel> BuildResxFiles(
      string projectDirectory,
      string defaultLocale,
      IReadOnlyDictionary<string, TranslationItemResponse[]> localeItems,
      bool prune
   )
   {
      var existingFiles = SafeScanProject(projectDirectory)
         .SourceFiles
         .ToDictionary(static file => (file.ResourceSetName, file.Locale), static file => file, EqualityComparer<(string, string?)>.Default);
      var keyAliases = BuildKeyAliases(existingFiles.Values);
      var knownResourceSets = existingFiles.Keys
         .Select(static key => key.Item1)
         .Distinct(StringComparer.Ordinal)
         .OrderByDescending(static name => name.Length)
         .ToArray();
      var grouped = new Dictionary<(string ResourceSetName, string? Locale), Dictionary<string, string?>>(EqualityComparer<(string, string?)>.Default);

      foreach (var localeGroup in localeItems)
      {
         foreach (var item in localeGroup.Value)
         {
            string resourceSetName;
            string key;

            try
            {
               (resourceSetName, key) = SplitApiKey(item.Key, knownResourceSets, keyAliases);
            }
            catch (Exception exception)
            {
               throw new InvalidOperationException(
                  $"Failed to map translation item. Locale: '{localeGroup.Key}'. Key: '{item.Key}'. Value: '{item.Value ?? string.Empty}'.",
                  exception
               );
            }

            var fileLocale = string.Equals(localeGroup.Key, defaultLocale, StringComparison.Ordinal) ? null : localeGroup.Key;
            var bucketKey = (resourceSetName, fileLocale);

            if (!grouped.TryGetValue(bucketKey, out var values))
            {
               values = new Dictionary<string, string?>(StringComparer.Ordinal);
               grouped[bucketKey] = values;
            }

            values[key] = item.Value;
         }
      }

      var result = new List<ResxFileModel>();
      foreach (var group in grouped.OrderBy(static x => x.Key.ResourceSetName, StringComparer.Ordinal).ThenBy(static x => x.Key.Locale, StringComparer.Ordinal))
      {
         existingFiles.TryGetValue((group.Key.ResourceSetName, group.Key.Locale), out var existingFile);
         var preservedComments = existingFile is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : _resxFileParser.Parse(existingFile.FilePath).Entries.ToDictionary(static entry => entry.Key, static entry => entry.Comment, StringComparer.Ordinal);
         var mergedEntries = prune
            ? group.Value
            : MergeExistingEntries(existingFile, group.Value);
         var filePath = existingFile?.FilePath ?? BuildFilePath(projectDirectory, group.Key.ResourceSetName, group.Key.Locale);

         var entries = mergedEntries
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ResxDataEntryModel
            {
               Key = entry.Key,
               Value = entry.Value,
               Comment = preservedComments.TryGetValue(entry.Key, out var comment) ? comment : null
            })
            .ToArray();

         if (entries.Length == 0 && group.Key.Locale is not null)
            continue;

         result.Add(new ResxFileModel
         {
            FilePath = filePath,
            Entries = entries
         });
      }

      return result;
   }

   private Dictionary<string, (string ResourceSetName, string Key)> BuildKeyAliases(IEnumerable<ResxMigrationSourceFile> files)
   {
      var aliases = new Dictionary<string, (string ResourceSetName, string Key)>(StringComparer.Ordinal);
      var ambiguousAliases = new HashSet<string>(StringComparer.Ordinal);

      foreach (var file in files)
      {
         foreach (var entry in _resxFileParser.Parse(file.FilePath).Entries)
         {
            var mapping = (file.ResourceSetName, entry.Key);
            var alias = NormalizeApiKeyAlias($"{file.ResourceSetName}.{entry.Key}");

            if (ambiguousAliases.Contains(alias))
               continue;

            if (aliases.TryGetValue(alias, out var existing) && existing != mapping)
            {
               aliases.Remove(alias);
               ambiguousAliases.Add(alias);
               continue;
            }

            aliases[alias] = mapping;
         }
      }

      return aliases;
   }

   private ResxMigrationScanResult SafeScanProject(string projectDirectory)
   {
      try
      {
         return _scanner.ScanProject(projectDirectory);
      }
      catch (InvalidOperationException) when (!Directory.EnumerateFiles(projectDirectory, "*.resx", SearchOption.AllDirectories).Any())
      {
         return new ResxMigrationScanResult
         {
            SourceFiles = [],
            HasBaseFiles = false
         };
      }
   }

   private static Dictionary<string, string?> MergeExistingEntries(ResxMigrationSourceFile? existingFile, IReadOnlyDictionary<string, string?> incomingValues)
   {
      var merged = existingFile is null
         ? new Dictionary<string, string?>(StringComparer.Ordinal)
         : new ResxFileParser().Parse(existingFile.FilePath).Entries.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);

      foreach (var item in incomingValues)
         merged[item.Key] = item.Value;

      return merged;
   }

   private IEnumerable<string> FindStaleFiles(string projectDirectory, IEnumerable<string> nextFiles)
   {
      var next = new HashSet<string>(nextFiles.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
      var existing = SafeScanProject(projectDirectory).SourceFiles.Select(static file => file.FilePath);

      foreach (var file in existing)
      {
         if (!next.Contains(Path.GetFullPath(file)))
            yield return file;
      }
   }

   private static (string ResourceSetName, string Key) SplitApiKey(
      string apiKey,
      IReadOnlyCollection<string> knownResourceSets,
      IReadOnlyDictionary<string, (string ResourceSetName, string Key)> keyAliases
   )
   {
      foreach (var resourceSetName in knownResourceSets)
      {
         if (!apiKey.StartsWith(resourceSetName + ".", StringComparison.Ordinal))
            continue;

         return (resourceSetName, apiKey.Substring(resourceSetName.Length + 1));
      }

      if (keyAliases.TryGetValue(apiKey, out var aliasMapping))
         return aliasMapping;

      if (knownResourceSets.Count == 1 && !apiKey.Contains('.', StringComparison.Ordinal))
         return (knownResourceSets.First(), apiKey);

      if (TrySplitLegacyNormalizedApiKey(apiKey, knownResourceSets, out var legacyMapping))
         return legacyMapping;

      var segments = apiKey.Split('.');
      if (segments.Length < 2)
         throw new InvalidOperationException($"Translation key '{apiKey}' cannot be mapped back to a .resx file.");

      if (segments.Length == 2)
         return (segments[0], segments[1]);

      return (string.Join(".", segments.Take(segments.Length - 2)), string.Join(".", segments.Skip(segments.Length - 2)));
   }

   private static bool TrySplitLegacyNormalizedApiKey(
      string apiKey,
      IReadOnlyCollection<string> knownResourceSets,
      out (string ResourceSetName, string Key) mapping
   )
   {
      foreach (var knownResourceSetName in knownResourceSets.OrderByDescending(static x => x.Length))
      {
         var resourceSetAlias = NormalizeApiKeyAlias(knownResourceSetName);
         if (!apiKey.StartsWith(resourceSetAlias + "_", StringComparison.Ordinal))
            continue;

         var keyAlias = apiKey.Substring(resourceSetAlias.Length + 1);
         if (string.IsNullOrWhiteSpace(keyAlias))
            continue;

         mapping = (knownResourceSetName, keyAlias);
         return true;
      }

      mapping = default;
      return false;
   }

   private static string NormalizeApiKeyAlias(string key)
   {
      var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (segments.Length == 0)
         return "_";

      return string.Join("_", segments.Select(NormalizeAliasSegment));
   }

   private static string NormalizeAliasSegment(string segment)
   {
      var builder = new System.Text.StringBuilder();
      var capitalizeNext = true;

      foreach (var character in segment)
      {
         if (!char.IsLetterOrDigit(character))
         {
            capitalizeNext = true;
            continue;
         }

         builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
         capitalizeNext = false;
      }

      if (builder.Length == 0)
         builder.Append('_');

      if (char.IsDigit(builder[0]))
         builder.Insert(0, '_');

      return builder.ToString();
   }

   private static string BuildFilePath(string projectDirectory, string resourceSetName, string? locale)
   {
      var relativePath = resourceSetName.Replace('.', Path.DirectorySeparatorChar);
      if (string.IsNullOrWhiteSpace(locale))
         return Path.Combine(projectDirectory, relativePath + ".resx");

      return Path.Combine(projectDirectory, relativePath + "." + locale + ".resx");
   }

   private static string FormatEntryDetails(IEnumerable<ResxDataEntryModel> entries)
   {
      return string.Join(
         " ",
         entries.Select(static entry => $"Key: '{entry.Key}'. Value: '{entry.Value ?? string.Empty}'.")
      );
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
   void DeleteFile(string path);
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

   public void DeleteFile(string path)
   {
      if (File.Exists(path))
         File.Delete(path);
   }
}
