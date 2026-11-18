using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal static partial class PluralParser
{
   [GeneratedRegex(@"^\{[ \t\r\n]*([a-z][a-zA-Z0-9]*)[ \t\r\n]*,[ \t\r\n]*plural[ \t\r\n]*,", RegexOptions.CultureInvariant)]
   private static partial Regex Header();

   [GeneratedRegex(@"\G(?:=[0-9]+|zero|one|two|few|many|other)", RegexOptions.CultureInvariant)]
   private static partial Regex Selector();

   [GeneratedRegex(@"\G\{[ \t\r\n]*[A-Za-z0-9_]+[ \t\r\n]*,", RegexOptions.CultureInvariant)]
   private static partial Regex NestedArgument();

   public static PluralValue? TryParse(string input)
   {
      if (string.IsNullOrEmpty(input) || input[0] != '{' || input[^1] != '}')
         return null;

      var header = Header().Match(input);
      if (!header.Success)
         return null;

      var cases = new List<PluralCase>();
      var selectors = new HashSet<string>(StringComparer.Ordinal);
      var i = header.Length;

      while (true)
      {
         i = SkipWhitespace(input, i);

         if (i < input.Length && input[i] == '}')
            break;

         var selector = Selector().Match(input, i);
         if (!selector.Success || !selectors.Add(selector.Value))
            return null;

         i = SkipWhitespace(input, i + selector.Length);
         if (i >= input.Length || input[i] != '{')
            return null;

         var close = FindFormEnd(input, i);
         if (close < 0)
            return null;

         var formText = input.Substring(i + 1, close - i - 1);
         cases.Add(new PluralCase(selector.Value, PlaceholderTokenParser.Parse(formText, numberSign: true)));
         i = close + 1;
      }

      if (cases.Count == 0 || i != input.Length - 1)
         return null;

      return new PluralValue(header.Groups[1].Value, cases);
   }

   private static int SkipWhitespace(string input, int i)
   {
      while (i < input.Length && input[i] is ' ' or '\t' or '\r' or '\n')
         i++;

      return i;
   }

   private static int FindFormEnd(string input, int open)
   {
      var depth = 0;
      var i = open;

      while (i < input.Length)
      {
         var c = input[i];

         if (c == '\'')
         {
            i = PlaceholderTokenParser.ReadApostrophe(input, i, numberSign: true, buffer: null);
            continue;
         }

         if (c == '{')
         {
            if (depth > 0 && NestedArgument().IsMatch(input, i))
               return -1;

            depth++;
         }
         else if (c == '}')
         {
            depth--;

            if (depth == 0)
               return i;
         }

         i++;
      }

      return -1;
   }
}
