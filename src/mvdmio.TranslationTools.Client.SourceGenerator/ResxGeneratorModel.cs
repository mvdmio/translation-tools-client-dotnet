using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal sealed class ResxGeneratorTypeModel
{
   public string Namespace { get; set; } = string.Empty;
   public string TypeName { get; set; } = string.Empty;
   public string ResourceBaseName { get; set; } = string.Empty;
   public ImmutableArray<ResxGeneratorPropertyModel> Properties { get; set; } = ImmutableArray<ResxGeneratorPropertyModel>.Empty;
}

internal sealed class ResxGeneratorPropertyModel
{
   public string Name { get; set; } = string.Empty;
   public string Key { get; set; } = string.Empty;
   public string ResourceKey { get; set; } = string.Empty;
   public string? DefaultValue { get; set; }
}

internal sealed class ResxGeneratorFileModel
{
   public ImmutableArray<ResxGeneratorTypeModel> Types { get; set; } = ImmutableArray<ResxGeneratorTypeModel>.Empty;
   public ImmutableArray<Diagnostic> Diagnostics { get; set; } = ImmutableArray<Diagnostic>.Empty;
}
