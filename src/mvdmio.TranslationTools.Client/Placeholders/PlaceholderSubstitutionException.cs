using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Thrown by the placeholder substitution engine when a placeholder error occurs and
/// <see cref="TranslationToolsClientOptions.ThrowOnPlaceholderError"/> is enabled.
/// </summary>
public sealed class PlaceholderSubstitutionException : Exception
{
   /// <summary>
   /// Create a placeholder substitution exception.
   /// </summary>
   public PlaceholderSubstitutionException(string message)
      : base(message)
   {
   }
}
