namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal sealed class ManifestPropertyDefinition
{
   public required string PropertyName { get; init; }
   public required string Key { get; init; }
   public required bool EmitExplicitKey { get; init; }
   public required string? DefaultValue { get; init; }
}
