using System.Text;
using mvdmio.TranslationTools.Client;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestFileBuilder
{
   public string Build(ManifestGenerationOptions options, IReadOnlyCollection<ManifestPropertyDefinition> properties)
   {
      var builder = new StringBuilder();

      builder.AppendLine("using mvdmio.TranslationTools.Client;");
      builder.AppendLine();

      if (!string.IsNullOrWhiteSpace(options.Namespace))
      {
         builder.Append("namespace ");
         builder.Append(options.Namespace);
         builder.AppendLine(";");
         builder.AppendLine();
      }

      builder.Append("[Translations(KeyNaming = TranslationKeyNaming.");
      builder.Append(options.KeyNaming);
      builder.AppendLine(")]");
      builder.Append("public static partial class ");
      builder.Append(options.ClassName);
      builder.AppendLine();
      builder.AppendLine("{");

      var orderedProperties = properties.ToArray();
      for (var index = 0; index < orderedProperties.Length; index++)
      {
         AppendProperty(builder, orderedProperties[index], "   ", Environment.NewLine);

         if (index < orderedProperties.Length - 1)
            builder.AppendLine();
      }

      builder.AppendLine("}");
      return builder.ToString();
   }

   private static string? BuildAttributeArguments(ManifestPropertyDefinition property)
   {
      var arguments = new List<string>();

      if (property.EmitExplicitKey)
         arguments.Add($"Key = {ToStringLiteral(property.Key)}");

      if (property.DefaultValue is not null)
         arguments.Add($"DefaultValue = {ToStringLiteral(property.DefaultValue)}");

      return arguments.Count == 0 ? null : string.Join(", ", arguments);
   }

   public string BuildPropertyBlock(ManifestPropertyDefinition property, string indentation, string newline)
   {
      var builder = new StringBuilder();
      AppendProperty(builder, property, indentation, newline);
      return builder.ToString();
   }

   private static void AppendProperty(StringBuilder builder, ManifestPropertyDefinition property, string indentation, string newline)
   {
      var attributeArguments = BuildAttributeArguments(property);

      if (attributeArguments is not null)
      {
         builder.Append(indentation);
         builder.Append("[Translation(");
         builder.Append(attributeArguments);
         builder.Append(")]");
         builder.Append(newline);
      }

      builder.Append(indentation);
      builder.Append("public static partial string ");
      builder.Append(property.PropertyName);
      builder.Append(" { get; }");
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
