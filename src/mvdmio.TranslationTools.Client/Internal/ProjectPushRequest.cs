using System;
using System.Text.Json.Serialization;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Body for <c>POST api/v1/translations/project</c>: the declared global placeholder names.
/// </summary>
internal sealed class ProjectPushRequest
{
   public required KeyValueItemRequest[] Items { get; init; } = Array.Empty<KeyValueItemRequest>();

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? Environment { get; init; }

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string[]? Globals { get; init; }
}

/// <summary>
/// One origin/locale/key value, shared by the project push and the Declaration.
/// </summary>
internal sealed class KeyValueItemRequest
{
   public required string Origin { get; init; }

   public required string Locale { get; init; }

   public required string Key { get; init; }

   public string? Value { get; init; }
}
