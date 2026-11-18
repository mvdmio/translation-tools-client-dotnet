using System;
using System.Text.Json.Serialization;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class DeclarationRequest
{
   public required KeyValueItemRequest[] Items { get; init; } = Array.Empty<KeyValueItemRequest>();

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? Environment { get; init; }
}
