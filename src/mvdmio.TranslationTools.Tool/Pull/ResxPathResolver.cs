namespace mvdmio.TranslationTools.Tool.Pull;

internal static class ResxPathResolver
{
   public static string BuildFilePath(
      string projectDirectory,
      string origin,
      string locale,
      string defaultLocale,
      IPullFileSystem fileSystem
   )
   {
      var normalizedOrigin = origin.Trim().Replace('\\', '/').TrimStart('/');
      var intendedBasePath = Path.Combine(projectDirectory, normalizedOrigin.Replace('/', Path.DirectorySeparatorChar));
      var directory = Path.GetDirectoryName(intendedBasePath) ?? projectDirectory;
      var intendedBaseName = Path.GetFileNameWithoutExtension(intendedBasePath);
      var existingFiles = fileSystem.EnumerateFiles(directory).ToArray();
      var existingBaseName = ResolveExistingBaseName(intendedBaseName, existingFiles) ?? intendedBaseName;

      if (string.Equals(locale, defaultLocale, StringComparison.OrdinalIgnoreCase))
         return ResolveExistingFilePath(Path.Combine(directory, existingBaseName + ".resx"), existingFiles);

      return ResolveExistingFilePath(Path.Combine(directory, existingBaseName + "." + locale + ".resx"), existingFiles);
   }

   private static string? ResolveExistingBaseName(string intendedBaseName, IReadOnlyCollection<string> existingFiles)
   {
      foreach (var file in existingFiles)
      {
         var fileName = Path.GetFileName(file);
         if (!fileName.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            continue;

         var stem = fileName[..^".resx".Length];
         if (string.Equals(stem, intendedBaseName, StringComparison.OrdinalIgnoreCase))
            return stem;

         if (stem.Length > intendedBaseName.Length + 1
             && stem[intendedBaseName.Length] == '.'
             && string.Equals(stem[..intendedBaseName.Length], intendedBaseName, StringComparison.OrdinalIgnoreCase))
         {
            return stem[..intendedBaseName.Length];
         }
      }

      return null;
   }

   private static string ResolveExistingFilePath(string intendedPath, IReadOnlyCollection<string> existingFiles)
   {
      var intendedDirectory = Path.GetDirectoryName(intendedPath);
      var intendedName = Path.GetFileName(intendedPath);

      foreach (var file in existingFiles)
      {
         if (!string.Equals(Path.GetFileName(file), intendedName, StringComparison.OrdinalIgnoreCase))
            continue;

         var fileDirectory = Path.GetDirectoryName(file);
         if (string.Equals(fileDirectory, intendedDirectory, StringComparison.OrdinalIgnoreCase)
             || (intendedDirectory is null && fileDirectory is null))
         {
            return file;
         }
      }

      return intendedPath;
   }
}
