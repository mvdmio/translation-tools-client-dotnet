using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Thrown by a single-key translation lookup when it fails and
/// <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> is enabled.
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
