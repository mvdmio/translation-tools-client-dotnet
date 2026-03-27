using System;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Marks a static partial class as a translation manifest target for source generation.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TranslationsAttribute : Attribute
{
   /// <summary>
   /// Gets or sets the naming policy used when a property does not define an explicit key.
   /// </summary>
   public TranslationKeyNaming KeyNaming { get; init; } = TranslationKeyNaming.LowerSnakeCase;
}
