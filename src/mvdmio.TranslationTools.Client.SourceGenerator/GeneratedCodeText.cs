using System.Reflection;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class GeneratedCodeText
{
   public static readonly string Version = typeof(GeneratedCodeText).Assembly
      .GetName().Version?.ToString()
      ?? typeof(GeneratedCodeText).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
      ?? "0.0.0.0";

   public static string ToStringLiteral(string value)
   {
      return "\"" + value
         .Replace("\\", "\\\\")
         .Replace("\"", "\\\"")
         .Replace("\r", "\\r")
         .Replace("\n", "\\n") + "\"";
   }
}
