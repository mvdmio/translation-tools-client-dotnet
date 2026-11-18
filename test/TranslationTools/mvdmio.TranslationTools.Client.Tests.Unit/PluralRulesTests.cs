using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Placeholders;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class PluralRulesTests
{
   private const string RULE_PREFIX = "pluralRule-count-";

   private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> _cardinalRules = new(LoadCardinalRules);

   public static TheoryData<string, string> LanguageCategories()
   {
      var data = new TheoryData<string, string>();
      foreach (var (language, rules) in _cardinalRules.Value)
      {
         foreach (var category in rules.Keys)
            data.Add(language, category);
      }

      return data;
   }

   [Theory]
   [MemberData(nameof(LanguageCategories))]
   public void GetCategory_ForEveryCldrIntegerSample_ReturnsTheCategoryCldrListsIt(string language, string category)
   {
      var mismatches = new List<string>();
      foreach (var number in IntegerSamples(_cardinalRules.Value[language][category]))
      {
         var actual = PluralRules.GetCategory(language, number).ToSelector();
         if (actual != category)
            mismatches.Add($"{language} {number}: expected {category} but got {actual}");
      }

      mismatches.Should().BeEmpty();
   }

   [Theory]
   [InlineData("en", 1, "one")]
   [InlineData("en", 5, "other")]
   [InlineData("en", 0, "other")]
   [InlineData("pl", 1, "one")]
   [InlineData("pl", 2, "few")]
   [InlineData("pl", 5, "many")]
   [InlineData("pl", 22, "few")]
   [InlineData("pl", 25, "many")]
   [InlineData("ja", 3, "other")]
   [InlineData("ar", 0, "zero")]
   [InlineData("ar", 2, "two")]
   public void GetCategory_ForKnownLanguage_ReturnsTheCldrCategory(string locale, long number, string expected)
   {
      PluralRules.GetCategory(locale, number).ToSelector().Should().Be(expected);
   }

   [Theory]
   [InlineData("pt-xx", 1, "one")]
   [InlineData("pt-xx", 0, "one")]
   [InlineData("pt-xx", 2, "other")]
   [InlineData("pt-PT", 0, "other")]
   [InlineData("pt-PT", 1, "one")]
   [InlineData("pl-PL", 22, "few")]
   public void GetCategory_ForRegionLocale_UsesTheLocaleThenItsLanguage(string locale, long number, string expected)
   {
      PluralRules.GetCategory(locale, number).ToSelector().Should().Be(expected);
   }

   [Theory]
   [InlineData("PL", 22, "few")]
   [InlineData("En", 1, "one")]
   [InlineData("PT-XX", 1, "one")]
   public void GetCategory_ForUpperCaseLocale_FindsTheRules(string locale, long number, string expected)
   {
      PluralRules.GetCategory(locale, number).ToSelector().Should().Be(expected);
   }

   [Theory]
   [InlineData(0)]
   [InlineData(1)]
   [InlineData(long.MaxValue)]
   public void GetCategory_ForUnknownLanguage_ReturnsOther(long number)
   {
      PluralRules.GetCategory("xx", number).ToSelector().Should().Be("other");
      PluralRules.GetCategory("xx-YY", number).ToSelector().Should().Be("other");
   }

   [Fact]
   public void GetCategory_ForLargestNumber_ReturnsACategoryForEveryLanguage()
   {
      var failures = new List<string>();
      foreach (var language in _cardinalRules.Value.Keys)
      {
         try
         {
            PluralRules.GetCategory(language, long.MaxValue);
         }
         catch (Exception exception)
         {
            failures.Add($"{language}: {exception.Message}");
         }
      }

      failures.Should().BeEmpty();
   }

   [Fact]
   public void GetCategory_ForFrenchMillions_ReturnsMany()
   {
      PluralRules.GetCategory("fr", 1_000_000).ToSelector().Should().Be("many");
      PluralRules.GetCategory("fr", 1_000_001).ToSelector().Should().Be("other");
   }

   private static Dictionary<string, Dictionary<string, string>> LoadCardinalRules()
   {
      using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plurals.json")));
      var cardinal = document.RootElement.GetProperty("supplemental").GetProperty("plurals-type-cardinal");

      var result = new Dictionary<string, Dictionary<string, string>>();
      foreach (var language in cardinal.EnumerateObject())
      {
         var categories = new Dictionary<string, string>();
         foreach (var rule in language.Value.EnumerateObject())
            categories[rule.Name[RULE_PREFIX.Length..]] = rule.Value.GetString()!;

         result[language.Name] = categories;
      }

      return result;
   }

   private static IEnumerable<long> IntegerSamples(string rule)
   {
      var integerStart = rule.IndexOf("@integer", StringComparison.Ordinal);
      if (integerStart < 0)
         yield break;

      var samples = rule[(integerStart + "@integer".Length)..].Split("@decimal")[0];
      foreach (var sample in samples.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
      {
         if (sample == "…" || sample.Contains('c') || sample.Contains('e'))
            continue;

         var bounds = sample.Split('~');
         for (var number = long.Parse(bounds[0]); number <= long.Parse(bounds[^1]); number++)
            yield return number;
      }
   }
}
