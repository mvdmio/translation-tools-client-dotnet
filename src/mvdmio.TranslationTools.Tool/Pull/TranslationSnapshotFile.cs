using System.Text.Json.Serialization;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class TranslationSnapshotFile
{
   [JsonPropertyName("schemaVersion")]
   public required int SchemaVersion { get; init; }

   [JsonPropertyName("project")]
   public required TranslationSnapshotProject Project { get; init; }

   [JsonPropertyName("translations")]
   public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Translations { get; init; }
}

internal sealed class TranslationSnapshotProject
{
   [JsonPropertyName("defaultLocale")]
   public required string DefaultLocale { get; init; }

   [JsonPropertyName("locales")]
   public required IReadOnlyCollection<string> Locales { get; init; }
}
