using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal sealed record PluralCase(string Selector, IReadOnlyList<PlaceholderTokenParser.Segment> Segments);

internal sealed record PluralValue(string Argument, IReadOnlyList<PluralCase> Cases)
{
   private const long MinValueMagnitudeResidue = long.MaxValue - 999_999;

   public IEnumerable<PlaceholderTokenParser.Segment> AllSegments => Cases.SelectMany(c => c.Segments);

   public PluralCase SelectForm(long? number, string locale)
   {
      if (number is not { } n)
         return ForSelector("other") ?? Cases[^1];

      var exact = Cases.FirstOrDefault(c => c.Selector[0] == '=' && long.TryParse(c.Selector.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var exactNumber) && exactNumber == n);
      if (exact is not null)
         return exact;

      var magnitude = n == long.MinValue ? MinValueMagnitudeResidue : Math.Abs(n);
      var category = PluralRules.GetCategory(locale, magnitude).ToSelector();

      return ForSelector(category) ?? ForSelector("other") ?? Cases[^1];
   }

   private PluralCase? ForSelector(string selector)
   {
      return Cases.FirstOrDefault(c => c.Selector == selector);
   }
}
