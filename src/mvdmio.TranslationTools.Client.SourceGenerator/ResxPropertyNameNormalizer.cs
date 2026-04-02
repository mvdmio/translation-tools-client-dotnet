using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class ResxPropertyNameNormalizer
{
   private static readonly HashSet<string> CSharpKeywords = [
      "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
      "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
      "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
      "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
      "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
      "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
      "volatile", "while"
   ];

   public static string Normalize(string key)
   {
      var segments = key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(static x => x.Trim())
         .Where(static x => x.Length > 0)
         .ToArray();
      if (segments.Length == 0)
         return "_";

      return string.Join("_", segments.Select(NormalizeSegment));
   }

   private static string NormalizeSegment(string segment)
   {
      var builder = new StringBuilder();
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

      var normalized = builder.ToString();
      if (CSharpKeywords.Contains(normalized.ToLowerInvariant()))
      {
         return normalized.Length == 1
            ? char.ToUpperInvariant(normalized[0]).ToString()
            : char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
      }

      return normalized;
   }
}
