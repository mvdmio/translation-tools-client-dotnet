using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Migrate;

internal sealed class ResxResourceSetParser
{
   public ResxParsedFile Parse(ResxMigrationSourceFile sourceFile)
   {
      var document = XDocument.Load(sourceFile.FilePath, LoadOptions.PreserveWhitespace);
      var entries = document.Root?
         .Elements("data")
         .Select(
            static element => new ResxParsedEntry
            {
               Key = (string?)element.Attribute("name") ?? string.Empty,
               Value = NormalizeValue(element.Element("value")?.Value)
            }
         )
         .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
         .ToArray() ?? [];

      var duplicateKeys = entries
         .GroupBy(static x => x.Key, StringComparer.Ordinal)
         .Where(static x => x.Count() > 1)
         .Select(static x => x.Key)
         .OrderBy(static x => x, StringComparer.Ordinal)
         .ToArray();

      if (duplicateKeys.Length > 0)
         throw new InvalidOperationException($"Duplicate keys in '{sourceFile.RelativePath}': {string.Join(", ", duplicateKeys)}.");

      return new ResxParsedFile
      {
         SourceFile = sourceFile,
         Entries = entries
      };
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }
}

internal sealed class ResxParsedFile
{
   public required ResxMigrationSourceFile SourceFile { get; init; }
   public required IReadOnlyCollection<ResxParsedEntry> Entries { get; init; }
}

internal sealed class ResxParsedEntry
{
   public required string Key { get; init; }
   public required string? Value { get; init; }
}
