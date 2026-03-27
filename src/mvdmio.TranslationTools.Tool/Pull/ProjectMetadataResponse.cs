namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class ProjectMetadataResponse
{
   public required string[] Locales { get; init; }
   public string? DefaultLocale { get; init; }
}
