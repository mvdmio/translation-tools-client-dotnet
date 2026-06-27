using System.Collections.Generic;
using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

/// <summary>
/// Tokenizer for placeholder values (generator-side copy of the canonical Phase 1 grammar). A token is
/// <c>{</c> + identifier + <c>}</c> where identifier matches <c>[a-z][a-zA-Z0-9]*</c>, with ICU-subset
/// apostrophe escaping restricted to literal braces.
/// </summary>
internal static class PlaceholderTokenParser
{
   /// <summary>
   /// Distinct token identifiers in first-seen order.
   /// </summary>
   public static List<string> TokenNames(string? input)
   {
      var result = new List<string>();
      if (string.IsNullOrEmpty(input))
         return result;

      var seen = new HashSet<string>();

      foreach (var name in Tokens(input!))
      {
         if (seen.Add(name))
            result.Add(name);
      }

      return result;
   }

   private static IEnumerable<string> Tokens(string input)
   {
      var i = 0;
      var length = input.Length;
      var buffer = new StringBuilder();

      while (i < length)
      {
         var c = input[i];

         if (c == '\'')
         {
            var next = i + 1 < length ? input[i + 1] : '\0';

            if (next == '\'')
            {
               i += 2;
               continue;
            }

            if (next == '{' || next == '}')
            {
               i += 1;
               while (i < length)
               {
                  if (input[i] == '\'')
                  {
                     if (i + 1 < length && input[i + 1] == '\'')
                     {
                        i += 2;
                        continue;
                     }

                     i += 1;
                     break;
                  }

                  i += 1;
               }

               continue;
            }

            i += 1;
            continue;
         }

         if (c == '{')
         {
            if (TryMatchToken(input, i, out var name, out var consumed))
            {
               yield return name;
               i += consumed;
               continue;
            }

            i += 1;
            continue;
         }

         i += 1;
      }
   }

   private static bool TryMatchToken(string input, int start, out string name, out int consumed)
   {
      name = string.Empty;
      consumed = 0;

      var length = input.Length;
      var i = start + 1;

      if (i >= length)
         return false;

      var first = input[i];
      if (first < 'a' || first > 'z')
         return false;

      var nameStart = i;
      i += 1;

      while (i < length)
      {
         var ch = input[i];
         var isIdentifierChar = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
         if (!isIdentifierChar)
            break;

         i += 1;
      }

      if (i >= length || input[i] != '}')
         return false;

      name = input.Substring(nameStart, i - nameStart);
      consumed = i - start + 1;
      return true;
   }
}
