using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Thrown by a translation lookup that fails and is not degraded to a local fallback, where the
/// failure produced no exception of its own to preserve: a whole-locale lookup, which always throws
/// rather than degrades, and a single-key lookup with
/// <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> enabled that timed out or was
/// suppressed after a recent connection failure, timeout, or 5xx.
///
/// A single-key lookup that reached the service and got an answer it could not use rethrows what
/// the fetch threw instead — an <see cref="System.Net.Http.HttpRequestException"/> or a
/// <see cref="System.Text.Json.JsonException"/> — so opting back into throwing restores the
/// exception types the client raised before the degrade-by-default contract existed.
/// </summary>
public sealed class TranslationLookupException : Exception
{
   /// <summary>
   /// Create a translation lookup exception.
   /// </summary>
   /// <param name="message">Why the lookup failed, naming the translation key and the effective locale.</param>
   /// <param name="innerException">The failure this one wraps, when there is one.</param>
   public TranslationLookupException(string message, Exception? innerException = null)
      : base(message, innerException)
   {
   }
}
