using mvdmio.TranslationTools.Client;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class TranslationPullRequest
{
   public required string ApiKey { get; init; }
   public required string OutputPath { get; init; }
   public required string? Namespace { get; init; }
   public required string ClassName { get; init; }
   public required TranslationKeyNaming KeyNaming { get; init; }
   public string? SharedKeyPrefix { get; init; }
}
