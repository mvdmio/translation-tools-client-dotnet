using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Overrides key metadata for a generated translation property.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property)]
public sealed class TranslationAttribute : Attribute
{
   /// <summary>
   /// Gets or sets the explicit translation key to use for the property.
   /// </summary>
   public string? Key { get; init; }

   /// <summary>
   /// Gets or sets the fallback value returned when the local cache has no value.
   /// </summary>
   public string? DefaultValue { get; init; }
}
