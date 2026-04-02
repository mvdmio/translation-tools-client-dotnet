using System.Xml.Linq;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class ResxFileWriter
{
   public string Write(ResxFileModel file)
   {
      var document = new XDocument(
         new XDeclaration("1.0", "utf-8", null),
         new XElement("root",
            CreateResHeader("resmimetype", "text/microsoft-resx"),
            CreateResHeader("version", "2.0"),
            CreateResHeader("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            CreateResHeader("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            file.Entries
               .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
               .Select(CreateDataElement)
         )
      );

      return document.ToString() + Environment.NewLine;
   }

   private static XElement CreateResHeader(string name, string value)
   {
      return new XElement("resheader",
         new XAttribute("name", name),
         new XElement("value", value)
      );
   }

   private static XElement CreateDataElement(ResxDataEntryModel entry)
   {
      var element = new XElement("data",
         new XAttribute("name", entry.Key),
         new XAttribute("xml:space", "preserve"),
         new XElement("value", entry.Value ?? string.Empty)
      );

      if (!string.IsNullOrWhiteSpace(entry.Comment))
         element.Add(new XElement("comment", entry.Comment));

      return element;
   }
}
