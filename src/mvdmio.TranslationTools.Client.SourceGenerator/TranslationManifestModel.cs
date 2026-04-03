using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal sealed class TranslationManifestModel
{
   public string Namespace { get; set; } = string.Empty;
   public string TypeName { get; set; } = string.Empty;
   public string Origin { get; set; } = string.Empty;
   public string Accessibility { get; set; } = string.Empty;
   public bool UsesCultureOverride { get; set; }
   public ImmutableArray<TranslationManifestPropertyModel> Properties { get; set; } = ImmutableArray<TranslationManifestPropertyModel>.Empty;
}

internal sealed class TranslationManifestPropertyModel
{
   public string Name { get; set; } = string.Empty;
   public string Key { get; set; } = string.Empty;
   public string? DefaultValue { get; set; }
}

internal sealed class TranslationManifestResult
{
   public TranslationManifestModel? Model { get; set; }
   public ImmutableArray<Diagnostic> Diagnostics { get; set; } = ImmutableArray<Diagnostic>.Empty;
}
