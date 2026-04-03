using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Stable identity for one translation entry.
/// </summary>
public readonly record struct TranslationRef
{
   /// <summary>
   /// Create a translation identity.
   /// </summary>
   public TranslationRef(string origin, string key)
   {
      Origin = Internal.TranslationClientInputValidator.ValidateOrigin(origin);
      Key = Internal.TranslationClientInputValidator.ValidateKey(key);
   }

   /// <summary>
   /// Resource-set origin.
   /// </summary>
   public string Origin { get; }

   /// <summary>
   /// Resource entry key.
   /// </summary>
   public string Key { get; }

   /// <inheritdoc />
   public bool Equals(TranslationRef other)
   {
      return StringComparer.OrdinalIgnoreCase.Equals(Origin, other.Origin)
             && StringComparer.Ordinal.Equals(Key, other.Key);
   }

   /// <inheritdoc />
   public override int GetHashCode()
   {
      return HashCode.Combine(
         StringComparer.OrdinalIgnoreCase.GetHashCode(Origin),
         StringComparer.Ordinal.GetHashCode(Key)
      );
   }
}
