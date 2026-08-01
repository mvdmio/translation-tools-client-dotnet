using System;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Liveness ping to <c>api/v1/translations/heartbeat</c>, telling the service which client, which
/// deployment environment, and which package version is running.
/// </summary>
internal sealed class HeartbeatRequest
{
   public Guid ClientId { get; init; }

   public string? Environment { get; init; }

   public required string Platform { get; init; }

   public required string Version { get; init; }
}
