using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mvdmio.TranslationTools.Client;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestFileParser
{
   public ManifestParseResult ParseDocument(string content, string className, TranslationKeyNaming keyNaming)
   {
      var syntaxTree = CSharpSyntaxTree.ParseText(content);
      var root = syntaxTree.GetRoot();
      var classDeclaration = root.DescendantNodes()
         .OfType<ClassDeclarationSyntax>()
         .FirstOrDefault(x => x.Identifier.ValueText == className);

      if (classDeclaration is null)
      {
         return new ManifestParseResult {
            ClassDeclaration = null,
            Properties = []
         };
      }

      var properties = classDeclaration.Members
         .OfType<PropertyDeclarationSyntax>()
         .Where(static x => x.Modifiers.Any(static modifier => modifier.Kind() == SyntaxKind.PartialKeyword))
         .Select(property => ParseProperty(property, keyNaming))
         .Where(static x => x is not null)
         .Cast<ManifestPropertyDefinition>()
         .ToArray();

      return new ManifestParseResult {
         ClassDeclaration = classDeclaration,
         Properties = properties
      };
   }

   public IReadOnlyCollection<ManifestPropertyDefinition> Parse(string content, string className, TranslationKeyNaming keyNaming)
   {
      return ParseDocument(content, className, keyNaming).Properties;
   }

   private static ManifestPropertyDefinition? ParseProperty(PropertyDeclarationSyntax property, TranslationKeyNaming keyNaming)
   {
      var propertyName = property.Identifier.ValueText;
      if (string.IsNullOrWhiteSpace(propertyName))
         return null;

      string? explicitKey = null;
      string? defaultValue = null;

      foreach (var attribute in property.AttributeLists.SelectMany(static x => x.Attributes))
      {
         var attributeName = attribute.Name.ToString();
         if (attributeName is not ("Translation" or "TranslationAttribute" or "mvdmio.TranslationTools.Client.Translation" or "mvdmio.TranslationTools.Client.TranslationAttribute"))
            continue;

         if (attribute.ArgumentList is null)
            break;

         foreach (var argument in attribute.ArgumentList.Arguments)
         {
            var name = argument.NameEquals?.Name.Identifier.ValueText;
            var value = GetStringValue(argument.Expression);

            if (name == "Key")
               explicitKey = value;

            if (name == "DefaultValue")
               defaultValue = value;
         }

         break;
      }

      return new ManifestPropertyDefinition {
         PropertyName = propertyName,
         Key = explicitKey ?? TranslationKeyNamingConverter.Convert(propertyName, (int)keyNaming),
         EmitExplicitKey = explicitKey is not null,
         DefaultValue = defaultValue
      };
   }

   private static string? GetStringValue(ExpressionSyntax expression)
   {
      if (expression is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression)
         return literal.Token.ValueText;

      return null;
   }
}

internal sealed class ManifestParseResult
{
   public required ClassDeclarationSyntax? ClassDeclaration { get; init; }
   public required IReadOnlyCollection<ManifestPropertyDefinition> Properties { get; init; }
}
