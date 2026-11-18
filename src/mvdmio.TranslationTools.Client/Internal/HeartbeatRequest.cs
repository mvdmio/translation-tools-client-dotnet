using System;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class HeartbeatRequest
{
   public Guid ClientId { get; init; }

   public string? Environment { get; init; }

   public required string Platform { get; init; }

   public required string Version { get; init; }
}
