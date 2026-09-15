using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace mvdmio.TranslationTools.Client.SourceGenerator;

internal sealed class TranslationManifestModel
{
   public string Namespace { get; set; } = string.Empty;
   public string TypeName { get; set; } = string.Empty;
   public string Origin { get; set; } = string.Empty;
   public string Accessibility { get; set; } = string.Empty;
   public bool UsesCultureOverride { get; set; }
   public ImmutableArray<TranslationManifestPropertyModel> Properties { get; set; } = ImmutableArray<TranslationManifestPropertyModel>.Empty;

   /// <summary>Declared global placeholder names discovered in the compilation. Included in every key's knownSet.</summary>
   public ImmutableArray<string> DeclaredGlobalNames { get; set; } = ImmutableArray<string>.Empty;
}

internal sealed class TranslationManifestPropertyModel
{
   public string Name { get; set; } = string.Empty;
   public string Key { get; set; } = string.Empty;
   public string? DefaultValue { get; set; }
   public ImmutableArray<TranslationManifestLocaleValueModel> LocaleValues { get; set; } = ImmutableArray<TranslationManifestLocaleValueModel>.Empty;

   /// <summary>Key-scoped token names (default-locale value tokens), globals excluded, first-seen order. Each becomes a required parameter.</summary>
   public ImmutableArray<string> Tokens { get; set; } = ImmutableArray<string>.Empty;

   /// <summary>True when the default-locale value contains any token (key-scoped or global). Drives method-vs-property emission.</summary>
   public bool HasTokens { get; set; }
}

internal sealed class TranslationManifestLocaleValueModel
{
   public string Locale { get; set; } = string.Empty;
   public string Value { get; set; } = string.Empty;
}

internal sealed class TranslationManifestResult
{
   public TranslationManifestModel? Model { get; set; }
   public ImmutableArray<Diagnostic> Diagnostics { get; set; } = ImmutableArray<Diagnostic>.Empty;
}
