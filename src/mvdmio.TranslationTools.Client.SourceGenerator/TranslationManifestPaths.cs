using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.IO;
using System.Linq;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationManifestPaths
{
   public static string BuildHintName(TranslationManifestModel model)
   {
      return string.IsNullOrWhiteSpace(model.Namespace)
         ? model.TypeName + ".Translations"
         : model.Namespace + "." + model.TypeName + ".Translations";
   }

   public static string BuildOrigin(string projectName, string relativePath)
   {
      var normalizedRelativePath = NormalizePath(relativePath);
      var fileName = GetFileName(normalizedRelativePath);
      var directory = GetDirectoryName(normalizedRelativePath);
      var baseFileName = TryGetLocaleSuffix(fileName, out var trimmedFileName, out _) ? trimmedFileName : fileName;
      var resourcePath = string.IsNullOrWhiteSpace(directory)
         ? "/" + baseFileName
         : "/" + directory + "/" + baseFileName;

      return projectName + ":" + resourcePath;
   }

   public static string BuildTypeName(string path)
   {
      var fileName = GetFileName(path);
      var baseFileName = TryGetLocaleSuffix(fileName, out var trimmedFileName, out _) ? trimmedFileName : fileName;
      return Path.GetFileNameWithoutExtension(baseFileName).Replace(".", string.Empty);
   }

   public static string BuildNamespace(string relativePath, string rootNamespace)
   {
      var sanitizedRootNamespace = string.Join(".", rootNamespace
         .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(SanitizeIdentifier)
      );

      var directory = GetDirectoryName(relativePath);
      if (string.IsNullOrWhiteSpace(directory))
         return sanitizedRootNamespace;

      var directorySegments = directory
         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(SanitizeIdentifier);

      if (string.IsNullOrWhiteSpace(sanitizedRootNamespace))
         return string.Join(".", directorySegments);

      return string.Join(".", new[] { sanitizedRootNamespace }.Concat(directorySegments));
   }

   public static string BuildProjectRelativePath(string path, string projectDirectory)
   {
      var normalizedPath = NormalizePath(path);
      var normalizedProjectDirectory = NormalizePath(projectDirectory);

      if (!IsAbsolutePath(path))
         return normalizedPath.TrimStart('/');

      if (!string.IsNullOrWhiteSpace(projectDirectory))
      {
         var projectPath = EnsureTrailingSeparator(normalizedProjectDirectory);
         var fullPath = normalizedPath;

         if (fullPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(projectPath.Length);
      }

      return GetFileName(normalizedPath);
   }

   public static bool IsValidProjectName(string projectName)
   {
      return !string.IsNullOrWhiteSpace(projectName) && !projectName.Contains(':');
   }

   public static string SanitizeIdentifier(string value)
   {
      if (string.IsNullOrWhiteSpace(value))
         return "Value";

      var characters = value.Select(static character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
      var identifier = new string(characters).Trim('_');
      if (string.IsNullOrWhiteSpace(identifier))
         identifier = "Value";

      if (!char.IsLetter(identifier[0]) && identifier[0] != '_')
         identifier = "_" + identifier;

      return identifier;
   }

   public static bool TryGetLocaleSuffix(string path, out string? baseFileName, out string? localeSuffix)
   {
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
      var separatorIndex = fileNameWithoutExtension.LastIndexOf('.');
      if (separatorIndex <= 0)
      {
         baseFileName = null;
         localeSuffix = null;
         return false;
      }

      var suffix = fileNameWithoutExtension.Substring(separatorIndex + 1);
      if (!suffix.All(static character => char.IsLetterOrDigit(character) || character == '-'))
      {
         baseFileName = null;
         localeSuffix = null;
         return false;
      }

      baseFileName = fileNameWithoutExtension.Substring(0, separatorIndex) + ".resx";
      localeSuffix = suffix;
      return true;
   }

   public static string NormalizePath(string path)
   {
      return path.Replace("\\", "/");
   }

   public static string GetFileName(string path)
   {
      var normalizedPath = NormalizePath(path);
      var separatorIndex = normalizedPath.LastIndexOf('/');
      return separatorIndex >= 0
         ? normalizedPath.Substring(separatorIndex + 1)
         : normalizedPath;
   }

   public static string GetDirectoryName(string path)
   {
      var normalizedPath = NormalizePath(path).TrimEnd('/');
      var separatorIndex = normalizedPath.LastIndexOf('/');
      if (separatorIndex <= 0)
         return string.Empty;

      return normalizedPath.Substring(0, separatorIndex);
   }

   public static string GetGlobalOption(AnalyzerConfigOptions options, string key)
   {
      return options.TryGetValue(key, out var value)
         ? value
         : string.Empty;
   }

   public static string GetProjectDirectory(AnalyzerConfigOptions options)
   {
      var projectDirectory = GetGlobalOption(options, "build_property.MSBuildProjectDirectory");
      if (!string.IsNullOrWhiteSpace(projectDirectory))
         return projectDirectory;

      return GetGlobalOption(options, "build_property.ProjectDir");
   }

   public static string GetProjectName(AnalyzerConfigOptions options)
   {
      var projectName = GetGlobalOption(options, "build_property.MSBuildProjectName");
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetGlobalOption(options, "build_property.ProjectName");
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.MSBuildProjectFile"));
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      projectName = GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.ProjectFileName"));
      if (!string.IsNullOrWhiteSpace(projectName))
         return projectName;

      return GetFileNameWithoutExtension(GetGlobalOption(options, "build_property.MSBuildProjectFullPath"));
   }

   private static bool IsAbsolutePath(string path)
   {
      if (string.IsNullOrWhiteSpace(path))
         return false;

      if (Path.IsPathRooted(path))
         return true;

      var normalizedPath = NormalizePath(path);
      return normalizedPath.Length >= 3
         && char.IsLetter(normalizedPath[0])
         && normalizedPath[1] == ':'
         && normalizedPath[2] == '/';
   }

   private static string EnsureTrailingSeparator(string path)
   {
      if (path.EndsWith("/", StringComparison.Ordinal))
         return path;

      return path + "/";
   }

   private static string GetFileNameWithoutExtension(string path)
   {
      var fileName = GetFileName(path);
      return string.IsNullOrWhiteSpace(fileName)
         ? string.Empty
         : Path.GetFileNameWithoutExtension(fileName);
   }
}
