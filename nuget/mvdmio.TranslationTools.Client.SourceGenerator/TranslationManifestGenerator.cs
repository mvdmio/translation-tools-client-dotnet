using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : IIncrementalGenerator
{
   private const string TranslationsAttributeName = "mvdmio.TranslationTools.Client.TranslationsAttribute";
   private const string TranslationAttributeName = "mvdmio.TranslationTools.Client.TranslationAttribute";

   public void Initialize(IncrementalGeneratorInitializationContext context)
   {
      var manifests = context.SyntaxProvider.ForAttributeWithMetadataName(
         TranslationsAttributeName,
         static (node, _) => node is ClassDeclarationSyntax,
         static (syntaxContext, _) => GetManifestResult(syntaxContext)
      );

      context.RegisterSourceOutput(manifests, static (productionContext, result) => {
         foreach (var diagnostic in result.Diagnostics)
            productionContext.ReportDiagnostic(diagnostic);

         if (result.Model is null)
            return;

         productionContext.AddSource(
            hintName: $"{BuildHintName(result.Model)}.g.cs",
            source: TranslationManifestEmitter.Emit(result.Model)
         );
      });
   }

   private static TranslationManifestResult GetManifestResult(GeneratorAttributeSyntaxContext syntaxContext)
   {
      var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
      var classDeclaration = (ClassDeclarationSyntax)syntaxContext.TargetNode;
      var classSymbol = (INamedTypeSymbol)syntaxContext.TargetSymbol;

      if (
         classSymbol.ContainingType is not null
         || classSymbol.TypeParameters.Length > 0
         || !classDeclaration.Modifiers.Any(static x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
      )
      {
         diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.InvalidTargetType, classDeclaration.Identifier.GetLocation(), classSymbol.Name));
         return new TranslationManifestResult {
            Diagnostics = diagnostics.ToImmutable()
         };
      }

      if (classSymbol.GetTypeMembers("Keys").Length > 0)
      {
         diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.ConflictingMemberName, classDeclaration.Identifier.GetLocation(), classSymbol.Name, "Keys"));
         return new TranslationManifestResult {
            Diagnostics = diagnostics.ToImmutable()
         };
      }

      if (classSymbol.GetMembers("Get").Length > 0)
      {
         diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.ConflictingMemberName, classDeclaration.Identifier.GetLocation(), classSymbol.Name, "Get"));
         return new TranslationManifestResult {
            Diagnostics = diagnostics.ToImmutable()
         };
      }

      var translationAttributeSymbol = syntaxContext.SemanticModel.Compilation.GetTypeByMetadataName(TranslationAttributeName);
      var propertySymbols = GetPropertySymbols(classSymbol, syntaxContext.SemanticModel.Compilation)
         .OrderBy(static x => x.Property.Locations[0].SourceSpan.Start)
         .ToArray();

      if (propertySymbols.Length == 0)
      {
         var hasAnyProperty = classSymbol.GetMembers().OfType<IPropertySymbol>().Any(static property => !property.IsImplicitlyDeclared);

         if (hasAnyProperty)
         {
            diagnostics.AddRange(
               classSymbol.GetMembers()
                  .OfType<IPropertySymbol>()
                  .Where(static property => !property.IsImplicitlyDeclared)
                  .Select(property => Diagnostic.Create(
                     TranslationGeneratorDiagnostics.InvalidManifestProperty,
                     property.Locations.FirstOrDefault(),
                     property.Name,
                     classSymbol.ToDisplayString()
                  ))
            );
         }
         else
         {
            diagnostics.Add(Diagnostic.Create(TranslationGeneratorDiagnostics.MissingManifest, classDeclaration.Identifier.GetLocation(), classSymbol.Name));
         }

         return new TranslationManifestResult {
            Diagnostics = diagnostics.ToImmutable()
         };
      }

      var keyNaming = GetKeyNaming(classSymbol);
      var usesCultureOverride = UsesCultureOverride(classSymbol);
      var properties = ImmutableArray.CreateBuilder<TranslationManifestPropertyModel>();

      foreach (var propertySymbol in propertySymbols)
      {
         if (!IsValidManifestProperty(propertySymbol.Property, propertySymbol.Syntax))
         {
            diagnostics.Add(
               Diagnostic.Create(
                  TranslationGeneratorDiagnostics.InvalidManifestProperty,
                  propertySymbol.Property.Locations.FirstOrDefault(),
                  propertySymbol.Property.Name,
                  classSymbol.ToDisplayString()
               )
            );

            continue;
         }

         if (properties.Any(x => x.Name == propertySymbol.Property.Name))
         {
            diagnostics.Add(
               Diagnostic.Create(
                  TranslationGeneratorDiagnostics.ConflictingMemberName,
                  propertySymbol.Property.Locations.FirstOrDefault(),
                  classSymbol.Name,
                  propertySymbol.Property.Name
               )
            );

            continue;
         }

         var translationAttribute = translationAttributeSymbol is null
            ? null
            : propertySymbol.Property.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, translationAttributeSymbol));
         var key = GetNamedAttributeValue(translationAttribute, "Key") as string;
         var defaultValue = GetNamedAttributeValue(translationAttribute, "DefaultValue") as string;

         properties.Add(new TranslationManifestPropertyModel {
            Name = propertySymbol.Property.Name,
            Accessibility = GetAccessibility(propertySymbol.Property.DeclaredAccessibility),
            IsStatic = propertySymbol.Property.IsStatic,
            Key = key ?? TranslationKeyNamingConverter.Convert(propertySymbol.Property.Name, keyNaming),
            DefaultValue = defaultValue
         });
      }

      if (diagnostics.Count > 0)
         return new TranslationManifestResult {
            Diagnostics = diagnostics.ToImmutable()
         };

      var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
         ? string.Empty
         : classSymbol.ContainingNamespace.ToDisplayString();

      return new TranslationManifestResult {
         Model = new TranslationManifestModel {
            Namespace = namespaceName,
            TypeName = classSymbol.Name,
            Accessibility = GetAccessibility(classSymbol.DeclaredAccessibility),
            IsStatic = classSymbol.IsStatic,
            UsesCultureOverride = usesCultureOverride,
            Properties = properties.ToImmutable()
         },
         Diagnostics = diagnostics.ToImmutable()
      };
   }

   private static bool IsValidManifestProperty(IPropertySymbol propertySymbol, PropertyDeclarationSyntax propertySyntax)
   {
      return propertySymbol.Type.SpecialType == SpecialType.System_String
             && propertySymbol.Parameters.Length == 0
             && propertySymbol.IsStatic
             && propertySymbol.GetMethod is not null
             && propertySymbol.SetMethod is null
             && propertySyntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword))
             && propertySyntax.ExpressionBody is null
             && propertySyntax.AccessorList is not null
             && propertySyntax.AccessorList.Accessors.Count == 1
             && propertySyntax.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration)
             && propertySyntax.AccessorList.Accessors[0].Body is null
             && propertySyntax.AccessorList.Accessors[0].ExpressionBody is null
             && propertySyntax.AccessorList.Accessors[0].SemicolonToken.IsKind(SyntaxKind.SemicolonToken);
   }

   private static ImmutableArray<PropertySymbolWithSyntax> GetPropertySymbols(INamedTypeSymbol classSymbol, Compilation compilation)
   {
      var properties = ImmutableArray.CreateBuilder<PropertySymbolWithSyntax>();

      foreach (var syntaxReference in classSymbol.DeclaringSyntaxReferences)
      {
         if (syntaxReference.GetSyntax() is not ClassDeclarationSyntax declaration)
            continue;

         var semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
         foreach (var propertySyntax in declaration.Members.OfType<PropertyDeclarationSyntax>())
         {
            if (!propertySyntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
               continue;

            if (semanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
               continue;

            properties.Add(new PropertySymbolWithSyntax(propertySymbol, propertySyntax));
         }
      }

      return properties.ToImmutable();
   }

   private static int GetKeyNaming(INamedTypeSymbol classSymbol)
   {
      var attribute = classSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == TranslationsAttributeName);
      var value = GetNamedAttributeValue(attribute, "KeyNaming");
      return value is int intValue ? intValue : 1;
   }

   private static object? GetNamedAttributeValue(AttributeData? attributeData, string propertyName)
   {
      if (attributeData is null)
         return null;

      foreach (var namedArgument in attributeData.NamedArguments)
      {
         if (namedArgument.Key == propertyName)
            return namedArgument.Value.Value;
      }

      return null;
   }

   private static string GetAccessibility(Accessibility accessibility)
   {
      return accessibility switch {
         Accessibility.Public => "public",
         Accessibility.Internal => "internal",
         _ => "internal"
      };
   }

   private static string BuildHintName(TranslationManifestModel model)
   {
      return string.IsNullOrWhiteSpace(model.Namespace)
         ? model.TypeName + ".Translations"
         : model.Namespace + "." + model.TypeName + ".Translations";
   }

   private static bool UsesCultureOverride(INamedTypeSymbol classSymbol)
   {
      return classSymbol.GetMembers("Culture")
         .OfType<IPropertySymbol>()
         .Any(static property =>
            property.IsStatic
            && property.Parameters.Length == 0
            && property.Type.Name == "CultureInfo"
            && property.Type.ContainingNamespace.ToDisplayString() == "System.Globalization"
         );
   }

   private sealed class PropertySymbolWithSyntax
   {
      public PropertySymbolWithSyntax(IPropertySymbol property, PropertyDeclarationSyntax syntax)
      {
         Property = property;
         Syntax = syntax;
      }

      public IPropertySymbol Property { get; }
      public PropertyDeclarationSyntax Syntax { get; }
   }
}
