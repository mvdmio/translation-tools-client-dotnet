using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestMergeResult
{
   public required string Content { get; init; }
   public required int AddedPropertyCount { get; init; }
   public required ClassDeclarationSyntax? ClassDeclaration { get; init; }
}
