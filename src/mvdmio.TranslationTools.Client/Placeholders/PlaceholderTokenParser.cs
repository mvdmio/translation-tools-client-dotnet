using System.Collections.Generic;
using System.Text;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Tokenizer for placeholder values. Implements the canonical Phase 1 grammar:
/// a token is <c>{</c> + identifier + <c>}</c> where identifier matches <c>[a-z][a-zA-Z0-9]*</c>,
/// with ICU-subset apostrophe escaping restricted to literal braces.
/// </summary>
internal static class PlaceholderTokenParser
{
   /// <summary>
   /// One parsed segment: either literal text or a token reference.
   /// </summary>
   internal readonly struct Segment
   {
      private Segment(bool isToken, string text)
      {
         IsToken = isToken;
         Text = text;
      }

      /// <summary>True when this segment is a token reference; false for literal text.</summary>
      public bool IsToken { get; }

      /// <summary>Literal text (for literals) or token identifier (for tokens).</summary>
      public string Text { get; }

      public static Segment Literal(string text) => new(false, text);
      public static Segment Token(string name) => new(true, name);
   }

   /// <summary>
   /// Parse <paramref name="input"/> into an ordered list of literal/token segments.
   /// </summary>
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

            // '' -> literal apostrophe.
            if (next == '\'')
            {
               buffer.Append('\'');
               i += 2;
               continue;
            }

            // Apostrophe immediately before { or } opens a quoted span where braces are literal.
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

                     // Lone apostrophe closes the span; consume it.
                     i += 1;
                     break;
                  }

                  buffer.Append(input[i]);
                  i += 1;
               }

               continue;
            }

            // Apostrophe before anything else is a literal apostrophe.
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

         // '}' and every other character are literal.
         buffer.Append(c);
         i += 1;
      }

      Flush();
      return segments;
   }

   /// <summary>
   /// Distinct token identifiers in first-seen order.
   /// </summary>
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

   /// <summary>
   /// Attempt to match a token starting at <paramref name="start"/> (which must point at '{').
   /// Matches <c>\{[a-z][a-zA-Z0-9]*\}</c>.
   /// </summary>
   private static bool TryMatchToken(string input, int start, out string name, out int consumed)
   {
      name = string.Empty;
      consumed = 0;

      var length = input.Length;
      var i = start + 1; // skip '{'

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
      consumed = i - start + 1; // include closing '}'
      return true;
   }
}
