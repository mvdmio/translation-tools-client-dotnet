using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Configuration;

internal sealed class ToolProjectContext
{
   public required string ProjectDirectory { get; init; }
   public required string ProjectFilePath { get; init; }
   public required string RootNamespace { get; init; }
}

internal static class ToolProjectResolver
{
   public static ToolProjectContext Resolve(ToolConfiguration config)
   {
      var projectFilePath = FindProjectFile(config.ConfigDirectory)
         ?? throw new InvalidOperationException($"Could not find a .csproj in '{config.ConfigDirectory}'. Place {ToolConfiguration.CONFIG_FILE_NAME} in the project root.");

      var projectDirectory = Path.GetDirectoryName(projectFilePath)!;
      if (!string.Equals(projectDirectory, config.ConfigDirectory, StringComparison.OrdinalIgnoreCase))
         throw new InvalidOperationException($"{ToolConfiguration.CONFIG_FILE_NAME} must be placed in the project root next to the .csproj. Expected under '{projectDirectory}'.");

      return new ToolProjectContext
      {
         ProjectDirectory = projectDirectory,
         ProjectFilePath = projectFilePath,
         RootNamespace = ReadRootNamespace(projectFilePath)
      };
   }

   private static string? FindProjectFile(string projectDirectory)
   {
      return Directory.GetFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
         .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
         .FirstOrDefault();
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
