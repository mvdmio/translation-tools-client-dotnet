using System;
using System.Text.Json.Serialization;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Body for <c>POST api/v1/translations/declaration</c>: every catalog key with the values the
/// <c>.resx</c> files hold. The service adds the keys to the Environment and stores the values by
/// Seeding. Each key expires after 7 days of Declarations without it.
/// </summary>
internal sealed class DeclarationRequest
{
   public required KeyValueItemRequest[] Items { get; init; } = Array.Empty<KeyValueItemRequest>();

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? Environment { get; init; }
}
