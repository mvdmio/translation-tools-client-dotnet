using System;
using System.Text.Json.Serialization;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Body for <c>POST api/v1/translations/project</c>: missing-key items and/or globals.
/// </summary>
internal sealed class ProjectPushRequest
{
   public required ProjectPushItemRequest[] Items { get; init; } = Array.Empty<ProjectPushItemRequest>();

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? Environment { get; init; }

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
   public bool Prune { get; init; }

   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string[]? Globals { get; init; }
}

/// <summary>
/// One origin/locale/key value in a project push.
/// </summary>
internal sealed class ProjectPushItemRequest
{
   public required string Origin { get; init; }

   public required string Locale { get; init; }

   public required string Key { get; init; }

   public string? Value { get; init; }
}
