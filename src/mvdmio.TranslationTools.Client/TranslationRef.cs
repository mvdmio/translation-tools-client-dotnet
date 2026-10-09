using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Stable identity for one translation entry.
/// Two values are equal when their origins and keys match without regard to case, as the service matches them.
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
   /// Origin, written as <c>&lt;project&gt;:&lt;path&gt;</c>.
   /// </summary>
   public string Origin { get; }

   /// <summary>
   /// Resource entry key, in the casing it was created with.
   /// Equality compares it without regard to case.
   /// </summary>
   public string Key { get; }

   /// <inheritdoc />
   public bool Equals(TranslationRef other)
   {
      return StringComparer.OrdinalIgnoreCase.Equals(Origin, other.Origin)
             && StringComparer.OrdinalIgnoreCase.Equals(Key, other.Key);
   }

   /// <inheritdoc />
   public override int GetHashCode()
   {
      return HashCode.Combine(
         StringComparer.OrdinalIgnoreCase.GetHashCode(Origin),
         StringComparer.OrdinalIgnoreCase.GetHashCode(Key)
      );
   }
}
