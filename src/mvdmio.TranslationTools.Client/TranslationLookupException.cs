using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Thrown by a translation lookup that fails and is not degraded to a local fallback: a
/// single-key lookup with <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> enabled,
/// or a whole-locale lookup, which always throws rather than degrades — including when a recent
/// connection failure, timeout, or 5xx has suppressed further calls to the service.
/// </summary>
public sealed class TranslationLookupException : Exception
{
   /// <summary>
   /// Create a translation lookup exception.
   /// </summary>
   public TranslationLookupException(string message, Exception? innerException = null)
      : base(message, innerException)
   {
   }
}
