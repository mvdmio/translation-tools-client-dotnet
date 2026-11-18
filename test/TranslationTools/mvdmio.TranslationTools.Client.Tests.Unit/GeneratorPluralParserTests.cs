using AwesomeAssertions;
using mvdmio.TranslationTools.Client.SourceGenerator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class GeneratorPluralParserTests
{
   public static TheoryData<string> ExampleInputs()
   {
      var data = new TheoryData<string>();

      foreach (var example in LoadExamples())
         data.Add(example.GetProperty("input").GetString()!);

      return data;
   }

   [Theory]
   [MemberData(nameof(ExampleInputs))]
   public void TryParse_ServerExample_MatchesExpectedParse(string input)
   {
      var example = LoadExamples().Single(e => e.GetProperty("input").GetString() == input);
      var expected = example.GetProperty("expected");

      var actual = PluralParser.TryParse(input);

      if (expected.ValueKind == JsonValueKind.Null)
      {
         actual.Should().BeNull();
         return;
      }

      actual.Should().NotBeNull();
      actual!.Argument.Should().Be(expected.GetProperty("argument").GetString());
      actual.Cases.Select(c => c.Selector).Should().Equal(expected.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("selector").GetString()));

      var expectedSegments = expected.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("segments").EnumerateArray().Select(DescribeExpected).ToList()).ToList();
      var actualSegments = actual.Cases.Select(c => c.Segments.Select(DescribeActual).ToList()).ToList();

      actualSegments.Should().HaveCount(expectedSegments.Count);
      for (var i = 0; i < expectedSegments.Count; i++)
         actualSegments[i].Should().Equal(expectedSegments[i]);
   }

   private static List<JsonElement> LoadExamples()
   {
      var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plural-parse-examples.json"));
      return JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
   }

   private static string DescribeExpected(JsonElement segment)
   {
      if (segment.TryGetProperty("literal", out var literal))
         return "literal:" + literal.GetString();

      return segment.TryGetProperty("token", out var token) ? "token:" + token.GetString() : "number";
   }

   private static string DescribeActual(PlaceholderTokenParser.Segment segment)
   {
      return segment.Kind switch
      {
         PlaceholderTokenParser.SegmentKind.Literal => "literal:" + segment.Text,
         PlaceholderTokenParser.SegmentKind.Token => "token:" + segment.Text,
         _ => "number",
      };
   }
}
