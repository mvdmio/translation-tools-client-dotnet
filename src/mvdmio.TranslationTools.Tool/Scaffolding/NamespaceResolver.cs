using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal static class NamespaceResolver
{
   public static string? Resolve(string outputFilePath)
   {
      var directory = Path.GetDirectoryName(outputFilePath);
      if (string.IsNullOrWhiteSpace(directory))
         return null;

      var csprojPath = FindNearestCsproj(directory);
      if (csprojPath is null)
         return null;

      var projectDirectory = Path.GetDirectoryName(csprojPath)!;
      var rootNamespace = ReadRootNamespace(csprojPath);
      var relativePath = Path.GetRelativePath(projectDirectory, directory);

      if (relativePath == ".")
         return rootNamespace;

      var namespaceSuffix = relativePath
         .Replace(Path.DirectorySeparatorChar, '.')
         .Replace(Path.AltDirectorySeparatorChar, '.');

      return $"{rootNamespace}.{namespaceSuffix}";
   }

   private static string? FindNearestCsproj(string startDirectory)
   {
      var directory = new DirectoryInfo(startDirectory);

      if (!directory.Exists)
         directory = directory.Parent ?? new DirectoryInfo(Path.GetPathRoot(startDirectory)!);

      while (directory is not null)
      {
         var csprojFile = directory.GetFiles("*.csproj").FirstOrDefault();
         if (csprojFile is not null)
            return csprojFile.FullName;

         directory = directory.Parent;
      }

      return null;
   }

   private static string ReadRootNamespace(string csprojPath)
   {
      var document = XDocument.Load(csprojPath);
      var rootNamespace = document.Descendants("RootNamespace").FirstOrDefault()?.Value;

      return string.IsNullOrWhiteSpace(rootNamespace)
         ? Path.GetFileNameWithoutExtension(csprojPath)
         : rootNamespace;
   }
}
