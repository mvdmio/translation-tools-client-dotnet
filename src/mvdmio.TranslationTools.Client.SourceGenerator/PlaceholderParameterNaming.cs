using System.Collections.Generic;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

public static class PlaceholderParameterNaming
{
   private static readonly HashSet<string> CSharpKeywords = new()
   {
      "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
      "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
      "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
      "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
      "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
      "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
      "volatile", "while"
   };

   public static string ToParameterIdentifier(string token)
   {
      return CSharpKeywords.Contains(token) ? "@" + token : token;
   }

   public static string ParameterKey(string token)
   {
      return ToParameterIdentifier(token).TrimStart('@');
   }

   public static (string Parameter, string First, string Second)? FindCollision(IEnumerable<string> tokens)
   {
      var byParameter = new Dictionary<string, string>();

      foreach (var token in tokens)
      {
         var parameter = ParameterKey(token);

         if (byParameter.TryGetValue(parameter, out var existing) && existing != token)
            return (parameter, existing, token);

         byParameter[parameter] = token;
      }

      return null;
   }
}
