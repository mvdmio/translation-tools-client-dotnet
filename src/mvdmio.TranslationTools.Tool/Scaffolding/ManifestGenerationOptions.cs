using mvdmio.TranslationTools.Client;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestGenerationOptions
{
   public required string? Namespace { get; init; }
   public required string ClassName { get; init; }
   public required TranslationKeyNaming KeyNaming { get; init; }
}
