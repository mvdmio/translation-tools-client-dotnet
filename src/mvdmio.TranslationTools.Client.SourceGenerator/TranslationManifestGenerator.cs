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

      var manifests = allResxFiles
         .Combine(analyzerOptions)
         .Combine(declaredGlobals)
         .SelectMany(static (input, cancellationToken) => TranslationManifestBuilder.BuildManifests(input.Left.Left, input.Left.Right, input.Right, cancellationToken));

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
   }
}
