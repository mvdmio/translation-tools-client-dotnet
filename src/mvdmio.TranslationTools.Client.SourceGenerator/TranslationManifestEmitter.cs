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

      builder.Append("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"mvdmio.TranslationTools.Client.SourceGenerator\", ");
      builder.Append(GeneratedCodeText.ToStringLiteral(GeneratedCodeText.Version));
      builder.Append(")]");
      builder.AppendLine();
      builder.Append(model.Accessibility);
      builder.Append(" static partial class ");

      builder.Append(model.TypeName);
      builder.AppendLine();
      builder.AppendLine("{");
      builder.Append("   private const string Origin = ");
      builder.Append(GeneratedCodeText.ToStringLiteral(model.Origin));
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
         builder.Append(GeneratedCodeText.ToStringLiteral(property.Key));
         builder.AppendLine("] = new global::System.Collections.Generic.Dictionary<string, string?>(global::System.StringComparer.OrdinalIgnoreCase) {");
         foreach (var localeValue in property.LocaleValues)
         {
            builder.Append("         [");
            builder.Append(GeneratedCodeText.ToStringLiteral(localeValue.Locale));
            builder.Append("] = ");
            builder.Append(GeneratedCodeText.ToStringLiteral(localeValue.Value));
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
         builder.Append(GeneratedCodeText.ToStringLiteral(property.Key));
         builder.AppendLine(");");
      }

      builder.AppendLine("   }");

      if (model.Properties.Length > 0)
         builder.AppendLine();

      foreach (var property in model.Properties)
      {
         if (property.HasTokens)
         {
            EmitPlaceholderMethod(builder, model, property);
            continue;
         }

         builder.Append("   public static string ");
         builder.Append(property.Name);
         builder.AppendLine();
         builder.AppendLine("   {");
         builder.Append("      get => Get(");
         builder.Append(GeneratedCodeText.ToStringLiteral(property.Key));

         if (property.DefaultValue is null)
         {
            builder.AppendLine(");");
            builder.AppendLine("   }");
            continue;
         }

         builder.Append(", ");
         builder.Append(GeneratedCodeText.ToStringLiteral(property.DefaultValue));
         builder.AppendLine(");");
         builder.AppendLine("   }");
      }

      builder.AppendLine("}");
      return builder.ToString();
   }

   private static void EmitPlaceholderMethod(StringBuilder builder, TranslationManifestModel model, TranslationManifestPropertyModel property)
   {
      // Method signature: one required string parameter per key-scoped token, first-seen order.
      builder.Append("   public static string ");
      builder.Append(property.Name);
      builder.Append("(");
      for (var i = 0; i < property.Tokens.Length; i++)
      {
         if (i > 0)
            builder.Append(", ");

         builder.Append("string ");
         builder.Append(PlaceholderParameterNaming.ToParameterIdentifier(property.Tokens[i]));
      }
      builder.AppendLine(")");
      builder.AppendLine("   {");

      // bindings dict: token name -> parameter.
      builder.Append("      var __bindings = new global::System.Collections.Generic.Dictionary<string, string?>(global::System.StringComparer.Ordinal) { ");
      for (var i = 0; i < property.Tokens.Length; i++)
      {
         if (i > 0)
            builder.Append(", ");

         builder.Append("[");
         builder.Append(GeneratedCodeText.ToStringLiteral(property.Tokens[i]));
         builder.Append("] = ");
         builder.Append(PlaceholderParameterNaming.ToParameterIdentifier(property.Tokens[i]));
      }
      builder.AppendLine(" };");

      // knownSet = key-scoped tokens ∪ declared global names.
      builder.Append("      var __knownSet = new string[] { ");
      var first = true;
      foreach (var token in property.Tokens)
      {
         if (!first)
            builder.Append(", ");

         builder.Append(GeneratedCodeText.ToStringLiteral(token));
         first = false;
      }
      foreach (var global in model.DeclaredGlobalNames)
      {
         if (!first)
            builder.Append(", ");

         builder.Append(GeneratedCodeText.ToStringLiteral(global));
         first = false;
      }
      builder.AppendLine(" };");

      var defaultValueLiteral = property.DefaultValue is null ? "null" : GeneratedCodeText.ToStringLiteral(property.DefaultValue);

      builder.Append("      return global::mvdmio.TranslationTools.Client.Translations.GetWithPlaceholders(new global::mvdmio.TranslationTools.Client.TranslationRef(Origin, ");
      builder.Append(GeneratedCodeText.ToStringLiteral(property.Key));
      builder.Append("), ");
      if (model.UsesCultureOverride)
         builder.Append("Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, ");
      builder.Append(defaultValueLiteral);
      builder.Append(", GetLocaleValues(");
      builder.Append(GeneratedCodeText.ToStringLiteral(property.Key));
      builder.AppendLine("), __bindings, __knownSet);");
      builder.AppendLine("   }");
   }
}
