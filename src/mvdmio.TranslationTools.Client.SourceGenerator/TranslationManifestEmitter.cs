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

      // Per-key locale value lookup table.
      builder.AppendLine("   private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, string?>> _localeValuesByKey = BuildLocaleValuesByKey();");
      builder.AppendLine();
      builder.AppendLine("   private static global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, string?>> BuildLocaleValuesByKey()");
      builder.AppendLine("   {");
      builder.AppendLine("      var result = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IReadOnlyDictionary<string, string?>>(global::System.StringComparer.Ordinal);");

      foreach (var property in model.Properties)
      {
         if (property.LocaleValues.Length == 0)
            continue;

         builder.Append("      result[");
         builder.Append(ToStringLiteral(property.Key));
         builder.AppendLine("] = new global::System.Collections.Generic.Dictionary<string, string?>(global::System.StringComparer.OrdinalIgnoreCase) {");
         foreach (var localeValue in property.LocaleValues)
         {
            builder.Append("         [");
            builder.Append(ToStringLiteral(localeValue.Locale));
            builder.Append("] = ");
            builder.Append(ToStringLiteral(localeValue.Value));
            builder.AppendLine(",");
         }
         builder.AppendLine("      };");
      }

      builder.AppendLine("      return result;");
      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   private static global::System.Collections.Generic.IReadOnlyDictionary<string, string?>? GetLocaleValues(string key)");
      builder.AppendLine("   {");
      builder.AppendLine("      return _localeValuesByKey.TryGetValue(key, out var values) ? values : null;");
      builder.AppendLine("   }");
      builder.AppendLine();

      builder.AppendLine("   public static string Get(string key, string? defaultValue = null)");
      builder.AppendLine("   {");

      if (model.UsesCultureOverride)
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translations.Get(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue, GetLocaleValues(key));");
      else
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translations.Get(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), defaultValue, GetLocaleValues(key));");

      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
      builder.AppendLine("   {");

      if (model.UsesCultureOverride)
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translations.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue, GetLocaleValues(key), cancellationToken);");
      else
         builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translations.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), defaultValue, GetLocaleValues(key), cancellationToken);");

      builder.AppendLine("   }");
      builder.AppendLine();
      builder.AppendLine("   public static global::System.Threading.Tasks.Task<string> GetAsync(string key, global::System.Globalization.CultureInfo locale, string? defaultValue = null, global::System.Threading.CancellationToken cancellationToken = default)");
      builder.AppendLine("   {");
      builder.AppendLine("      return global::mvdmio.TranslationTools.Client.Translations.GetAsync(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, key), locale, defaultValue, GetLocaleValues(key), cancellationToken);");

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
