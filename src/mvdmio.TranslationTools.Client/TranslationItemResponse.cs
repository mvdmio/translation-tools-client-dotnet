namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Translation item returned by the TranslationTools API.
/// </summary>
public sealed class TranslationItemResponse
{
   /// <summary>
   /// Translation key.
   /// </summary>
   public required string Key { get; init; }

   /// <summary>
   /// Translation value.
   /// </summary>
   public required string? Value { get; init; }
}
