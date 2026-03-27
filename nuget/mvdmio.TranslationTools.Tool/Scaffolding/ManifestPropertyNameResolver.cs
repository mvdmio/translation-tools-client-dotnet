namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal static class ManifestPropertyNameResolver
{
   public static string Resolve(string key)
   {
      if (key.IndexOf('_', StringComparison.Ordinal) >= 0 && key.IndexOf('.', StringComparison.Ordinal) < 0)
         return key;

      var groups = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
      if (groups.Length == 0)
         return "Key";

      var resolvedGroups = groups.Select(ResolveGroup).Where(static x => x.Length > 0).ToArray();
      var propertyName = string.Join("_", resolvedGroups);

      if (string.IsNullOrWhiteSpace(propertyName))
         propertyName = "Key";

      if (!IsIdentifierStart(propertyName[0]))
         propertyName = "Key_" + propertyName;

      return propertyName;
   }

   private static string ResolveGroup(string group)
   {
      var words = new List<string>();
      var current = new List<char>();

      foreach (var character in group)
      {
         if (char.IsLetterOrDigit(character))
         {
            current.Add(character);
            continue;
         }

         FlushCurrentWord(words, current);
      }

      FlushCurrentWord(words, current);

      return string.Concat(words.Select(ToPascalCase));
   }

   private static void FlushCurrentWord(List<string> words, List<char> current)
   {
      if (current.Count == 0)
         return;

      words.Add(new string([.. current]));
      current.Clear();
   }

   private static string ToPascalCase(string word)
   {
      if (string.IsNullOrWhiteSpace(word))
         return string.Empty;

      if (word.Length == 1)
         return char.ToUpperInvariant(word[0]).ToString();

      return char.ToUpperInvariant(word[0]) + word[1..];
   }

   private static bool IsIdentifierStart(char character)
   {
      return character == '_' || char.IsLetter(character);
   }
}
