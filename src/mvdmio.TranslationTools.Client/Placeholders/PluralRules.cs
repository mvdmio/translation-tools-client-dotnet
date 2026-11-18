namespace mvdmio.TranslationTools.Client.Placeholders;

internal static partial class PluralRules
{
   public static PluralCategory GetCategory(string locale, long number)
   {
      var name = locale.ToLowerInvariant();
      if (Evaluate(name, number) is { } category)
         return category;

      var separator = name.IndexOf('-');
      if (separator > 0 && Evaluate(name[..separator], number) is { } languageCategory)
         return languageCategory;

      return PluralCategory.Other;
   }
}
