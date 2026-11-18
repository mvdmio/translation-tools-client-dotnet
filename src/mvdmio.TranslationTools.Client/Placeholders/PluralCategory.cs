namespace mvdmio.TranslationTools.Client.Placeholders;

internal enum PluralCategory
{
   Zero,
   One,
   Two,
   Few,
   Many,
   Other,
}

internal static class PluralCategoryExtensions
{
   public static string ToSelector(this PluralCategory category) => category switch
   {
      PluralCategory.Zero => "zero",
      PluralCategory.One => "one",
      PluralCategory.Two => "two",
      PluralCategory.Few => "few",
      PluralCategory.Many => "many",
      _ => "other",
   };
}
