using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Scaffolding;

namespace mvdmio.TranslationTools.Tool.Push;

internal sealed class ProjectManifestScanner
{
   public ProjectManifestScanResult ScanProject(string projectDirectory)
   {
      if (!Directory.Exists(projectDirectory))
         throw new DirectoryNotFoundException($"Project directory not found: {projectDirectory}");

      var scanResults = Directory
         .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
         .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
         .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
         .SelectMany(ScanFile)
         .ToArray();

      if (scanResults.Length == 0)
      {
         return new ProjectManifestScanResult {
            FoundManifest = false,
            Items = []
         };
      }

      var definitions = new Dictionary<string, ProjectTranslationPushItem>(StringComparer.Ordinal);

      foreach (var item in scanResults.Where(static x => x.Item is not null).Select(static x => x.Item!))
      {
         if (!definitions.TryAdd(item.Key, item))
            definitions[item.Key] = Merge(definitions[item.Key], item);
      }

      return new ProjectManifestScanResult {
         FoundManifest = scanResults.Any(static x => x.FoundManifest),
         Items = definitions.Values.OrderBy(static x => x.Key, StringComparer.Ordinal).ToArray()
      };
   }

   private static IEnumerable<ProjectManifestScanItem> ScanFile(string filePath)
   {
      var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
      var root = syntaxTree.GetRoot();

      foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
      {
         if (!TryGetKeyNaming(classDeclaration, out var keyNaming))
            continue;

         var yieldedProperty = false;

         foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
         {
            if (!property.Modifiers.Any(static modifier => modifier.Kind() == SyntaxKind.PartialKeyword))
               continue;

            var item = ParseProperty(property, keyNaming);
            if (item is not null)
            {
               yieldedProperty = true;
               yield return new ProjectManifestScanItem {
                  FoundManifest = true,
                  Item = item
               };
            }
         }

         if (!yieldedProperty)
         {
            yield return new ProjectManifestScanItem {
               FoundManifest = true,
               Item = null
            };
         }
      }
   }

   private static bool TryGetKeyNaming(ClassDeclarationSyntax classDeclaration, out TranslationKeyNaming keyNaming)
   {
      keyNaming = TranslationKeyNaming.LowerSnakeCase;

      foreach (var attribute in classDeclaration.AttributeLists.SelectMany(static x => x.Attributes))
      {
         var attributeName = attribute.Name.ToString();
         if (attributeName is not ("Translations" or "TranslationsAttribute" or "mvdmio.TranslationTools.Client.Translations" or "mvdmio.TranslationTools.Client.TranslationsAttribute"))
            continue;

         if (attribute.ArgumentList is null)
            return true;

         foreach (var argument in attribute.ArgumentList.Arguments)
         {
            if (argument.NameEquals?.Name.Identifier.ValueText != nameof(TranslationsAttribute.KeyNaming))
               continue;

            if (TryParseKeyNaming(argument.Expression, out var parsedKeyNaming))
               keyNaming = parsedKeyNaming;
         }

         return true;
      }

      return false;
   }

   private static bool TryParseKeyNaming(ExpressionSyntax expression, out TranslationKeyNaming keyNaming)
   {
      var value = expression.ToString().Split('.').LastOrDefault();
      return Enum.TryParse(value, ignoreCase: false, out keyNaming);
   }

   private static ProjectTranslationPushItem? ParseProperty(PropertyDeclarationSyntax property, TranslationKeyNaming keyNaming)
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

            if (name == nameof(TranslationAttribute.Key))
               explicitKey = value;

            if (name == nameof(TranslationAttribute.DefaultValue))
               defaultValue = value;
         }

         break;
      }

      return new ProjectTranslationPushItem {
         Key = explicitKey ?? TranslationKeyNamingConverter.Convert(propertyName, (int)keyNaming),
         DefaultValue = defaultValue
      };
   }

   private static string? GetStringValue(ExpressionSyntax expression)
   {
      if (expression is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression)
         return literal.Token.ValueText;

      return null;
   }

   private static ProjectTranslationPushItem Merge(ProjectTranslationPushItem existing, ProjectTranslationPushItem incoming)
   {
      var existingHasValue = !string.IsNullOrWhiteSpace(existing.DefaultValue);
      var incomingHasValue = !string.IsNullOrWhiteSpace(incoming.DefaultValue);

      if (existingHasValue && incomingHasValue && !string.Equals(existing.DefaultValue, incoming.DefaultValue, StringComparison.Ordinal))
         throw new InvalidOperationException($"Duplicate translation key '{incoming.Key}' has conflicting default values.");

      return incomingHasValue ? incoming : existing;
   }
}

internal sealed class ProjectManifestScanResult
{
   public required bool FoundManifest { get; init; }
   public required IReadOnlyCollection<ProjectTranslationPushItem> Items { get; init; }
}

internal sealed class ProjectManifestScanItem
{
   public required bool FoundManifest { get; init; }
   public required ProjectTranslationPushItem? Item { get; init; }
}
