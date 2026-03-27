using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationManifestEmitter
{
   public static string Emit(TranslationManifestModel model)
   {
      var builder = new StringBuilder();
      builder.AppendLine("#nullable enable");

      if (!string.IsNullOrWhiteSpace(model.Namespace))
      {
         builder.Append("namespace ");
         builder.Append(model.Namespace);
         builder.AppendLine(";");
         builder.AppendLine();
      }

      builder.Append("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"mvdmio.TranslationTools.Client.SourceGenerator\", \"0.1.0\")]");
      builder.AppendLine();
      builder.Append(model.Accessibility);

      if (model.IsStatic)
         builder.Append(" static partial class ");
      else
         builder.Append(" partial class ");

      builder.Append(model.TypeName);
      builder.AppendLine();
      builder.AppendLine("{");
      builder.AppendLine("   private static string Get(string key, string? defaultValue = null)");
      builder.AppendLine("   {");

      if (model.UsesCultureOverride)
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translate.Get(key, Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue);");
      else
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translate.Get(key, defaultValue);");

      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static class Keys");
      builder.AppendLine("   {");

      foreach (var property in model.Properties)
      {
         builder.Append("      public const string ");
         builder.Append(property.Name);
         builder.Append(" = ");
         builder.Append(ToStringLiteral(property.Key));
         builder.AppendLine(";");
      }

      builder.AppendLine("   }");

      if (model.Properties.Length > 0)
         builder.AppendLine();

      foreach (var property in model.Properties)
      {
         builder.Append("   ");
         builder.Append(property.Accessibility);

         if (property.IsStatic)
            builder.Append(" static");

         builder.Append(" partial string ");
         builder.Append(property.Name);
         builder.AppendLine();
         builder.AppendLine("   {");
         builder.Append("      get => Get(Keys.");
         builder.Append(property.Name);

         if (property.DefaultValue is null)
         {
            builder.AppendLine(");");
            builder.AppendLine("   }");
            continue;
         }

         builder.Append(", ");
         builder.Append(ToStringLiteral(property.DefaultValue));
         builder.AppendLine(");");
         builder.AppendLine("   }");
      }

      builder.AppendLine("}");
      return builder.ToString();
   }

   private static string ToStringLiteral(string value)
   {
      return "\"" + value
         .Replace("\\", "\\\\")
         .Replace("\"", "\\\"")
         .Replace("\r", "\\r")
         .Replace("\n", "\\n") + "\"";
   }
}
