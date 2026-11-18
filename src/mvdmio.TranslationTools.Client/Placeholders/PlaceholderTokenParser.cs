using System.Collections.Generic;
using System.Text;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal static class PlaceholderTokenParser
{
   internal readonly struct Segment
   {
      private Segment(bool isToken, string text)
      {
         IsToken = isToken;
         Text = text;
      }

      public bool IsToken { get; }

      public string Text { get; }

      public static Segment Literal(string text) => new(false, text);
      public static Segment Token(string name) => new(true, name);
   }

   public static List<Segment> Parse(string input)
   {
      var segments = new List<Segment>();
      if (string.IsNullOrEmpty(input))
         return segments;

      var buffer = new StringBuilder();

      void Flush()
      {
         if (buffer.Length > 0)
         {
            segments.Add(Segment.Literal(buffer.ToString()));
            buffer.Clear();
         }
      }

      var i = 0;
      var length = input.Length;

      while (i < length)
      {
         var c = input[i];

         if (c == '\'')
         {
            var next = i + 1 < length ? input[i + 1] : '\0';

            if (next == '\'')
            {
               buffer.Append('\'');
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
                        buffer.Append('\'');
                        i += 2;
                        continue;
                     }

                     i += 1;
                     break;
                  }

                  buffer.Append(input[i]);
                  i += 1;
               }

               continue;
            }

            buffer.Append('\'');
            i += 1;
            continue;
         }

         if (c == '{')
         {
            if (TryMatchToken(input, i, out var name, out var consumed))
            {
               Flush();
               segments.Add(Segment.Token(name));
               i += consumed;
               continue;
            }

            buffer.Append('{');
            i += 1;
            continue;
         }

         buffer.Append(c);
         i += 1;
      }

      Flush();
      return segments;
   }

   public static List<string> TokenNames(string input)
   {
      var seen = new HashSet<string>(System.StringComparer.Ordinal);
      var result = new List<string>();

      foreach (var segment in Parse(input))
      {
         if (segment.IsToken && seen.Add(segment.Text))
            result.Add(segment.Text);
      }

      return result;
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
