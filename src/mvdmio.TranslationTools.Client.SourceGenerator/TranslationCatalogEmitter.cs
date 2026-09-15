using System.Linq;
using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationCatalogEmitter
{
   public static string Emit(TranslationCatalogModel model)
   {
      var builder = new StringBuilder();
      builder.AppendLine("#nullable enable");
      builder.AppendLine();
      builder.AppendLine("namespace mvdmio.TranslationTools.Client.Generated;");
      builder.AppendLine();
      builder.Append("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"mvdmio.TranslationTools.Client.SourceGenerator\", ");
      builder.Append(GeneratedCodeText.ToStringLiteral(GeneratedCodeText.Version));
      builder.AppendLine(")]");
      builder.AppendLine("internal static class TranslationCatalogRegistration");
      builder.AppendLine("{");
      builder.AppendLine("   [global::System.Runtime.CompilerServices.ModuleInitializerAttribute]");
      builder.AppendLine("   internal static void Initialize()");
      builder.AppendLine("   {");
      builder.AppendLine("      global::mvdmio.TranslationTools.Client.TranslationCatalog.Register(");

      for (var i = 0; i < model.Entries.Length; i++)
      {
         var entry = model.Entries[i];
         builder.Append("         new global::mvdmio.TranslationTools.Client.TranslationCatalogKey(");
         builder.Append(GeneratedCodeText.ToStringLiteral(entry.Origin));
         builder.Append(", ");
         builder.Append(GeneratedCodeText.ToStringLiteral(entry.Key));
         builder.Append(", ");
         builder.Append(entry.NeutralValue is null ? "null" : GeneratedCodeText.ToStringLiteral(entry.NeutralValue));

         if (entry.LocaleValues.Length > 0)
         {
            builder.Append(", new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.OrdinalIgnoreCase) {");
            foreach (var localeValue in entry.LocaleValues.OrderBy(static x => x.Locale, System.StringComparer.OrdinalIgnoreCase))
            {
               builder.Append(" [");
               builder.Append(GeneratedCodeText.ToStringLiteral(localeValue.Locale));
               builder.Append("] = ");
               builder.Append(GeneratedCodeText.ToStringLiteral(localeValue.Value));
               builder.Append(",");
            }
            builder.Append(" }");
         }

         builder.Append(')');
         builder.AppendLine(i == model.Entries.Length - 1 ? string.Empty : ",");
      }

      builder.AppendLine("      );");
      builder.AppendLine("   }");
      builder.AppendLine("}");
      return builder.ToString();
   }
}
