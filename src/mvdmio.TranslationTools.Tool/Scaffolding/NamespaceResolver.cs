using mvdmio.TranslationTools.Tool.Configuration;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal static class NamespaceResolver
{
   public static string? Resolve(string outputFilePath, ToolProjectContext projectContext)
   {
      var directory = Path.GetDirectoryName(outputFilePath);
      if (string.IsNullOrWhiteSpace(directory))
         return null;

      var relativePath = Path.GetRelativePath(projectContext.ProjectDirectory, directory);

      if (relativePath == ".")
         return projectContext.RootNamespace;

      var namespaceSuffix = relativePath
         .Replace(Path.DirectorySeparatorChar, '.')
         .Replace(Path.AltDirectorySeparatorChar, '.');

      return $"{projectContext.RootNamespace}.{namespaceSuffix}";
   }
}
