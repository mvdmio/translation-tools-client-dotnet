using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var scriptDirectory = ScriptDirectory();
var sourcePath = Path.Combine(scriptDirectory, "plurals.json");
var targetPath = Path.GetFullPath(Path.Combine(scriptDirectory, "..", "src", "mvdmio.TranslationTools.Client", "Placeholders", "PluralRules.g.cs"));

string[] categories = ["zero", "one", "two", "few", "many"];

using var document = JsonDocument.Parse(File.ReadAllText(sourcePath));
var supplemental = document.RootElement.GetProperty("supplemental");
var cldrVersion = supplemental.GetProperty("version").GetProperty("_cldrVersion").GetString();
if (cldrVersion != "49")
   throw new InvalidOperationException($"Expected CLDR 49 but plurals.json is CLDR {cldrVersion}.");

var localesByBody = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
foreach (var language in supplemental.GetProperty("plurals-type-cardinal").EnumerateObject().OrderBy(l => l.Name.ToLowerInvariant(), StringComparer.Ordinal))
{
   var branches = new List<string>();
   var fallback = "PluralCategory.Other";
   foreach (var category in categories)
   {
      if (!language.Value.TryGetProperty($"pluralRule-count-{category}", out var rule))
         continue;

      var condition = Compile(rule.GetString()!);
      if (condition.Value == false)
         continue;

      if (condition.Value == true)
      {
         fallback = $"PluralCategory.{Capitalize(category)}";
         break;
      }

      branches.Add($"{condition.Code} ? PluralCategory.{Capitalize(category)}");
   }

   var body = string.Join("\n            : ", branches.Append(fallback));

   if (!localesByBody.TryGetValue(body, out var locales))
      localesByBody[body] = locales = [];

   locales.Add(language.Name.ToLowerInvariant());
}

var builder = new StringBuilder();
builder.Append("namespace mvdmio.TranslationTools.Client.Placeholders;\n\n");
builder.Append("internal static partial class PluralRules\n{\n");
builder.Append("   private static PluralCategory? Evaluate(string language, long n) => language switch\n   {\n");
foreach (var (body, locales) in localesByBody.OrderBy(entry => entry.Value.Min(), StringComparer.Ordinal))
{
   var labels = string.Join("\n         or ", locales.Order(StringComparer.Ordinal).Select(locale => $"\"{locale}\""));
   builder.Append($"      {labels} =>\n         {body},\n");
}

builder.Append("      _ => null,\n   };\n}\n");

File.WriteAllText(targetPath, builder.ToString(), new UTF8Encoding(false));
return;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static string Capitalize(string value) => char.ToUpperInvariant(value[0]) + value[1..];

static Condition Compile(string rule)
{
   var text = rule.Split('@')[0].Trim();
   if (text.Length == 0)
      return Condition.True;

   var alternatives = text.Split(" or ").Select(group => All(group.Split(" and ").Select(Relation).ToList())).ToList();
   return Any(alternatives);
}

static Condition All(List<Condition> conditions)
{
   if (conditions.Any(c => c.Value == false))
      return Condition.False;

   var open = conditions.Where(c => c.Value is null).ToList();
   return open.Count switch
   {
      0 => Condition.True,
      1 => open[0],
      _ => new Condition(null, string.Join(" && ", open.Select(c => c.Code))),
   };
}

static Condition Any(List<Condition> conditions)
{
   if (conditions.Any(c => c.Value == true))
      return Condition.True;

   var open = conditions.Where(c => c.Value is null).ToList();
   return open.Count switch
   {
      0 => Condition.False,
      1 => open[0],
      _ => new Condition(null, string.Join(" || ", open.Select(c => c.Code.Contains(" && ") ? $"({c.Code})" : c.Code))),
   };
}

static Condition Relation(string text)
{
   var match = Regex.Match(text.Trim(), @"^(?<operand>[nivwftce])(?: % (?<modulus>\d+))? (?<operator>!?=) (?<ranges>\d+(?:\.\.\d+)?(?:,\d+(?:\.\.\d+)?)*)$");
   if (!match.Success)
      throw new InvalidOperationException($"Cannot read plural relation '{text}'.");

   var negated = match.Groups["operator"].Value == "!=";
   var ranges = match.Groups["ranges"].Value.Split(',').Select(range =>
   {
      var bounds = range.Split("..");
      return (Low: long.Parse(bounds[0]), High: long.Parse(bounds[^1]));
   }).ToList();

   if (match.Groups["operand"].Value is not ("n" or "i"))
   {
      var matches = ranges.Any(range => range.Low <= 0 && 0 <= range.High);
      return matches != negated ? Condition.True : Condition.False;
   }

   var subject = match.Groups["modulus"].Success ? $"n % {match.Groups["modulus"].Value}" : "n";
   var patterns = ranges.Select(range => range.Low == range.High ? $"{range.Low}" : $">= {range.Low} and <= {range.High}").ToList();
   var pattern = patterns.Count == 1 ? patterns[0] : string.Join(" or ", patterns.Select(p => p.Contains(' ') ? $"({p})" : p));

   if (!negated)
      return new Condition(null, $"{subject} is {pattern}");

   return new Condition(null, patterns.Count == 1 && !pattern.Contains(' ') ? $"{subject} is not {pattern}" : $"{subject} is not ({pattern})");
}

internal readonly record struct Condition(bool? Value, string Code)
{
   public static Condition True { get; } = new(true, "true");

   public static Condition False { get; } = new(false, "false");
}
