using System.Reflection;
using System.Text;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal static class TranslationManifestEmitter
{
   private static readonly string GeneratedCodeVersion = typeof(TranslationManifestEmitter).Assembly
      .GetName().Version?.ToString()
      ?? typeof(TranslationManifestEmitter).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
      ?? typeof(TranslationManifestEmitter).Assembly.GetName().Version?.ToString()
      ?? "0.0.0.0";

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

      builder.Append("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"mvdmio.TranslationTools.Client.SourceGenerator\", ");
      builder.Append(ToStringLiteral(GeneratedCodeVersion));
      builder.Append(")]");
      builder.AppendLine();
       builder.Append(model.Accessibility);
       builder.Append(" static partial class ");

       builder.Append(model.TypeName);
       builder.AppendLine();
       builder.AppendLine("{");
       builder.Append("   private const string Origin = ");
       builder.Append(ToStringLiteral(model.Origin));
       builder.AppendLine(";");
       builder.AppendLine();
       builder.AppendLine("   public static string Get(string key, string? defaultValue = null)");
       builder.AppendLine("   {");

       if (model.UsesCultureOverride)
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationToolsClient.Get(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue);");
       else
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationToolsClient.Get(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), defaultValue);");

       builder.AppendLine("   }");
       builder.AppendLine();
       builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
       builder.AppendLine("   {");

       if (model.UsesCultureOverride)
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationToolsClient.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue, cancellationToken);");
       else
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationToolsClient.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), defaultValue, cancellationToken);");

       builder.AppendLine("   }");
       builder.AppendLine();
       builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, global::System.Globalization.CultureInfo locale, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
       builder.AppendLine("   {");
       builder.AppendLine("      return global::mvdmio.TranslationTools.Client.TranslationToolsClient.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), locale, defaultValue, cancellationToken);");

      builder.AppendLine("   }");
      builder.AppendLine();
       builder.AppendLine("   public static class Keys");
       builder.AppendLine("   {");

       foreach (var property in model.Properties)
       {
          builder.Append("      public static readonly global::mvdmio.TranslationTools.Client.TranslationRef ");
          builder.Append(property.Name);
          builder.Append(" = new(Origin, ");
          builder.Append(ToStringLiteral(property.Key));
          builder.AppendLine(");");
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
         builder.Append(ToStringLiteral(property.Key));

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
