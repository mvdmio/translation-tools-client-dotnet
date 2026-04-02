using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class ResxFileParser
{
   public ResxFileModel Parse(string filePath)
   {
      var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
      var entries = document.Root?
         .Elements("data")
         .Select(element => new ResxDataEntryModel
         {
            Key = (string?)element.Attribute("name") ?? string.Empty,
            Value = NormalizeValue(element.Element("value")?.Value),
            Comment = element.Element("comment")?.Value
         })
         .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
         .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
         .ToArray() ?? [];

      return new ResxFileModel
      {
         FilePath = filePath,
         Entries = entries
      };
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }
}
