namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Naming policies used to derive translation keys from manifest property names.
/// </summary>
public enum TranslationKeyNaming
{
   /// <summary>
   /// Use the property name as-is.
   /// </summary>
   PropertyName = 0,

   /// <summary>
   /// Convert the property name to lower snake case.
   /// </summary>
   LowerSnakeCase = 1,

   /// <summary>
   /// Replace underscores in the property name with dots.
   /// </summary>
   UnderscoreToDot = 2
}
