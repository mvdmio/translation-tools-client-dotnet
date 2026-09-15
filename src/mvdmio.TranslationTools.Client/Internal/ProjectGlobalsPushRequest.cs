using System;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Runtime globals-only push to <c>api/v1/translations/project</c>.
/// Empty <see cref="Items"/> leaves keys untouched (liveness-only); a non-null <see cref="Globals"/>
/// full-replaces this Environment's declared global placeholder names.
/// </summary>
internal sealed class ProjectGlobalsPushRequest
{
   public required object[] Items { get; init; } = Array.Empty<object>();

   public string? Environment { get; init; }

   public required string[] Globals { get; init; }
}
