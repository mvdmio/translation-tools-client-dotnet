using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class ResxGeneratorEmitter
{
   public static string Emit(ResxGeneratorTypeModel model)
   {
      var builder = new StringBuilder();
      builder.AppendLine("#nullable enable");
      builder.Append("namespace ");
      builder.Append(model.Namespace);
      builder.AppendLine(";");
      builder.AppendLine();
      builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"mvdmio.TranslationTools.Client.SourceGenerator\", \"0.1.0\")]");
      builder.Append("public static partial class ");
      builder.Append(model.TypeName);
      builder.AppendLine();
      builder.AppendLine("{");
      builder.AppendLine("   private static global::System.Type ManifestType => typeof(" + model.TypeName + ");");
      builder.AppendLine();
      builder.AppendLine("   public static string Get(string key, string? defaultValue = null)");
      builder.AppendLine("   {");
      builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationManifestRuntime.Get(ManifestType, key, Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue);");
      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
      builder.AppendLine("   {");
      builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationManifestRuntime.GetAsync(ManifestType, key, Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue, cancellationToken);");
      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, global::System.Globalization.CultureInfo locale, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
      builder.AppendLine("   {");
      builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationManifestRuntime.GetAsync(ManifestType, key, locale, defaultValue, cancellationToken);");
      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static global::System.Globalization.CultureInfo? Culture { get; set; }");
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
         builder.Append("   public static string ");
         builder.Append(property.Name);
         builder.AppendLine();
         builder.AppendLine("   {");
         builder.Append("      get => Get(");
         builder.Append(ToStringLiteral(property.ResourceKey));

         if (property.DefaultValue is null)
         {
            builder.AppendLine(");");
         }
         else
         {
            builder.Append(", ");
            builder.Append(ToStringLiteral(property.DefaultValue));
            builder.AppendLine(");");
         }

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
