using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Provides a stable client identifier for this application instance.
/// </summary>
public interface IClientIdStore
{
   /// <summary>
   /// Returns a stable GUID that identifies this client across restarts.
   /// Implementations persist the value where possible; when no persistent
   /// store is available a fresh per-process GUID is returned instead.
   /// Repeated calls on the same instance return the same value.
   /// </summary>
   Guid GetOrCreateClientId();
}
