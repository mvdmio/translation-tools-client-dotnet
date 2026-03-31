using System.Text.RegularExpressions;

namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed partial class ResxMigrationScanner
{
   public ResxMigrationScanResult ScanProject(string projectDirectory)
   {
      var files = Directory.EnumerateFiles(projectDirectory, "*.resx", SearchOption.AllDirectories)
         .Where(static path => !IsIgnoredPath(path))
         .Select(path => CreateSourceFile(projectDirectory, path))
         .OrderBy(static x => x.RelativePath, StringComparer.Ordinal)
         .ToArray();

      if (files.Length == 0)
         throw new InvalidOperationException($"No .resx locale files found in project '{projectDirectory}'.");

      ValidateSourceFiles(files);

      return new ResxMigrationScanResult {
         SourceFiles = files,
         HasBaseFiles = files.Any(static x => x.Locale is null)
      };
   }

   internal static void ValidateSourceFiles(IReadOnlyCollection<ResxMigrationSourceFile> files)
   {
      if (files.Count == 0)
         return;

      var resourceSetCollisions = files
         .GroupBy(static x => x.ResourceSetName, StringComparer.Ordinal)
         .Select(
            static group => new {
               group.Key,
               BasePaths = group.Select(static x => x.ResourceSetPath).Distinct(StringComparer.Ordinal).ToArray()
            }
         )
         .Where(static x => x.BasePaths.Length > 1)
         .ToArray();

      if (resourceSetCollisions.Length > 0)
      {
         var details = string.Join(", ", resourceSetCollisions.Select(x => $"{x.Key} ({string.Join(", ", x.BasePaths)})"));
         throw new InvalidOperationException($"Resource-set prefix collision detected: {details}.");
      }

      foreach (var resourceSet in files.GroupBy(static x => x.ResourceSetName, StringComparer.Ordinal))
      {
         var duplicateLocaleGroups = resourceSet
            .Where(static x => x.Locale is not null)
            .GroupBy(static x => x.Locale!, StringComparer.Ordinal)
            .Where(static x => x.Count() > 1)
            .ToArray();

         if (duplicateLocaleGroups.Length == 0)
            continue;

         var duplicateDetails = string.Join(", ", duplicateLocaleGroups.Select(x => $"locale '{x.Key}' -> {string.Join(", ", x.Select(static y => y.RelativePath))}"));
         throw new InvalidOperationException($"Multiple .resx files normalize to the same locale for resource set '{resourceSet.Key}': {duplicateDetails}.");
      }
   }

   private static bool IsIgnoredPath(string path)
   {
      return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
         .Any(static segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
   }

   private static ResxMigrationSourceFile CreateSourceFile(string projectDirectory, string filePath)
   {
      var relativePath = Path.GetRelativePath(projectDirectory, filePath);
      var relativeDirectory = Path.GetDirectoryName(relativePath);
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
      var fileNameStem = Path.GetFileNameWithoutExtension(filePath);
      string? baseName = fileNameWithoutExtension;
      string? locale = null;

      if (TrySplitLocaleSuffix(fileNameStem, out var fileBaseName))
      {
         locale = NormalizeLocale(fileNameStem[(fileBaseName!.Length + 1)..]);
         baseName = fileBaseName;
      }

      var resourceSetPath = string.IsNullOrWhiteSpace(relativeDirectory)
         ? baseName
         : Path.Combine(relativeDirectory, baseName);

      return new ResxMigrationSourceFile {
         FilePath = filePath,
         RelativePath = relativePath,
         ResourceSetPath = resourceSetPath,
         ResourceSetName = resourceSetPath.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
         Locale = locale
      };
   }

   internal static string NormalizeLocale(string locale)
   {
      ArgumentException.ThrowIfNullOrWhiteSpace(locale);
      return locale.Trim().ToLowerInvariant();
   }

   private static bool TrySplitLocaleSuffix(string fileNameStem, out string? baseName)
   {
      var separatorIndex = fileNameStem.LastIndexOf('.');
      if (separatorIndex <= 0)
      {
         baseName = null;
         return false;
      }

      var suffix = fileNameStem[(separatorIndex + 1)..];
      if (!LocaleSuffixPattern().IsMatch(suffix))
      {
         baseName = null;
         return false;
      }

      baseName = fileNameStem[..separatorIndex];
      return true;
   }

   [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
   private static partial Regex LocaleSuffixPattern();
}

internal sealed class ResxMigrationScanResult
{
   public required IReadOnlyCollection<ResxMigrationSourceFile> SourceFiles { get; init; }
   public required bool HasBaseFiles { get; init; }
}

internal sealed class ResxMigrationSourceFile
{
   public required string FilePath { get; init; }
   public required string RelativePath { get; init; }
   public required string ResourceSetPath { get; init; }
   public required string ResourceSetName { get; init; }
   public required string? Locale { get; init; }
}
