using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

[Generator]
public sealed class TranslationManifestGenerator : IIncrementalGenerator
{
   public void Initialize(IncrementalGeneratorInitializationContext context)
   {
      var analyzerOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => new TranslationManifestBuilder.GeneratorOptions
      {
         ProjectDirectory = TranslationManifestPaths.GetProjectDirectory(provider.GlobalOptions),
         ProjectName = TranslationManifestPaths.GetProjectName(provider.GlobalOptions),
         RootNamespace = TranslationManifestPaths.GetGlobalOption(provider.GlobalOptions, "build_property.RootNamespace")
      });

      var allResxFiles = context.AdditionalTextsProvider
         .Where(file => file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
         .Collect();

      var declaredGlobals = context.SyntaxProvider
         .ForAttributeWithMetadataName(
            "mvdmio.TranslationTools.Client.GlobalPlaceholderAttribute",
            predicate: static (_, _) => true,
            transform: static (ctx, _) => TranslationManifestBuilder.ResolveGlobalNames(ctx))
         .SelectMany(static (names, _) => names)
         .Collect();

      var builds = allResxFiles
         .Combine(analyzerOptions)
         .Combine(declaredGlobals)
         .Select(static (input, cancellationToken) => TranslationManifestBuilder.Build(input.Left.Left, input.Left.Right, input.Right, cancellationToken));

      var manifests = builds.SelectMany(static (build, _) => build.Manifests);
      var catalogs = builds.Select(static (build, _) => build.Catalog);

      context.RegisterSourceOutput(manifests, static (productionContext, result) =>
      {
         foreach (var diagnostic in result.Diagnostics)
            productionContext.ReportDiagnostic(diagnostic);

         if (result.Model is null)
            return;

         productionContext.AddSource(
            hintName: TranslationManifestPaths.BuildHintName(result.Model) + ".g.cs",
            source: TranslationManifestEmitter.Emit(result.Model)
          );
      });

      context.RegisterSourceOutput(catalogs, static (productionContext, catalog) =>
      {
         if (catalog is null || catalog.Entries.IsDefaultOrEmpty)
            return;

         productionContext.AddSource(
            hintName: "TranslationCatalog.g.cs",
            source: TranslationCatalogEmitter.Emit(catalog)
         );
      });
   }
}
