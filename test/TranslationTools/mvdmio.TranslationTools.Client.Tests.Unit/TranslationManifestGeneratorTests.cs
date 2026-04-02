using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using mvdmio.TranslationTools.Client.SourceGenerator;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationManifestGeneratorTests
{
   [Fact]
   public void ShouldGenerateKeysAndPropertiesFromResx()
   {
      var result = RunGenerator(
         "namespace Demo { }",
         ("D:\\Project\\Errors.resx", Resx(("save.button", "Save"), ("class", "Class value")))
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSources.Should().ContainSingle();
      result.GeneratedSources[0].Should().Contain("public static partial class Errors");
      result.GeneratedSources[0].Should().Contain("public const string Save_Button = \"Errors.save.button\";");
      result.GeneratedSources[0].Should().Contain("public const string Class = \"Errors.class\";");
      result.GeneratedSources[0].Should().Contain("public static string Save_Button");
      result.GeneratedSources[0].Should().Contain("get => Get(\"save.button\", \"Save\");");
   }

   [Fact]
   public void ShouldGenerateNestedNamespaceFromRelativeFolder()
   {
      var result = RunGenerator(
         "namespace Demo { }",
         ("D:\\Project\\Admin\\Labels.resx", Resx(("title", "Admin")))
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSources.Should().ContainSingle();
      result.GeneratedSources[0].Should().Contain("namespace GeneratorTests.Admin;");
      result.GeneratedSources[0].Should().Contain("public static partial class Labels");
      result.GeneratedSources[0].Should().Contain("public const string Title = \"Admin.Labels.title\";");
   }

   [Fact]
   public void ShouldReportConflictingPropertyNamesAfterNormalization()
   {
      var result = RunGenerator(
         "namespace Demo { }",
         ("D:\\Project\\Errors.resx", Resx(("save.button", "Save"), ("save.button", "Save 2")))
      );

      result.GeneratorDiagnostics.Select(static x => x.Id).Should().Contain("TTCLIENTGEN002");
   }

   [Fact]
   public void ShouldReferenceStableRoslynAssemblies()
   {
      var references = typeof(TranslationManifestGenerator).Assembly.GetReferencedAssemblies();

      references.Select(static x => x.Name).Should().Contain("Microsoft.CodeAnalysis");
   }

   [Fact]
   public void ShouldGenerateForDottedBaseFileNames()
   {
      var result = RunGenerator(
         "namespace Demo { }",
         ("D:\\Project\\Shared.Validation.resx", Resx(("required", "Required")))
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSources.Should().ContainSingle();
      result.GeneratedSources[0].Should().Contain("public static partial class Shared.Validation");
   }

   private static GeneratorTestResult RunGenerator(string source, params (string Path, string Content)[] additionalFiles)
   {
      var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
      var compilation = CSharpCompilation.Create(
         assemblyName: "GeneratorTests",
         syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
         references: GetReferences(),
         options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
      );

      var analyzerConfig = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>(StringComparer.Ordinal)
      {
         ["build_property.MSBuildProjectDirectory"] = "D:\\Project",
         ["build_property.RootNamespace"] = "GeneratorTests"
      });
      GeneratorDriver driver = CSharpGeneratorDriver.Create(
         generators: [new TranslationManifestGenerator()],
         additionalTexts: [.. additionalFiles.Select(static file => new TestAdditionalText(file.Path, file.Content))],
         parseOptions: parseOptions,
         optionsProvider: analyzerConfig
      );

      driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var outputDiagnostics);
      var runResult = driver.GetRunResult();

      return new GeneratorTestResult(
         GeneratedSources: [.. runResult.Results.SelectMany(static x => x.GeneratedSources).Select(static x => x.SourceText.ToString())],
         GeneratorDiagnostics: runResult.Results.SelectMany(static x => x.Diagnostics).ToImmutableArray(),
         CompilationDiagnostics: [.. outputCompilation.GetDiagnostics(), .. outputDiagnostics]
      );
   }

   private static MetadataReference[] GetReferences()
   {
      var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
         .Split(Path.PathSeparator)
         .Select(path => MetadataReference.CreateFromFile(path))
         .ToList();
      trustedPlatformAssemblies.Add(MetadataReference.CreateFromFile(typeof(TranslationManifestGenerator).Assembly.Location));
      return [.. trustedPlatformAssemblies.DistinctBy(static x => x.Display, StringComparer.OrdinalIgnoreCase)];
   }

   private static string Resx(params (string Key, string Value)[] entries)
   {
      var lines = new List<string> {
         "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
         "<root>"
      };

      foreach (var entry in entries)
      {
         lines.Add($"  <data name=\"{entry.Key}\" xml:space=\"preserve\">");
         lines.Add($"    <value>{entry.Value}</value>");
         lines.Add("  </data>");
      }

      lines.Add("</root>");
      return string.Join(Environment.NewLine, lines);
   }

   private sealed record GeneratorTestResult(
      ImmutableArray<string> GeneratedSources,
      ImmutableArray<Diagnostic> GeneratorDiagnostics,
      ImmutableArray<Diagnostic> CompilationDiagnostics
   );

   private sealed class TestAdditionalText : AdditionalText
   {
      private readonly SourceText _sourceText;

      public TestAdditionalText(string path, string content)
      {
         Path = path;
         _sourceText = SourceText.From(content);
      }

      public override string Path { get; }

      public override SourceText GetText(CancellationToken cancellationToken = default)
      {
         return _sourceText;
      }
   }

   private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
   {
      private readonly AnalyzerConfigOptions _globalOptions;

      public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
      {
         _globalOptions = new TestAnalyzerConfigOptions(values);
      }

      public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

      public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
      {
         return new TestAnalyzerConfigOptions(new Dictionary<string, string>());
      }

      public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
      {
         return new TestAnalyzerConfigOptions(new Dictionary<string, string>());
      }
   }

   private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
   {
      private readonly IReadOnlyDictionary<string, string> _values;

      public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
      {
         _values = values;
      }

      public override bool TryGetValue(string key, out string value)
      {
         return _values.TryGetValue(key, out value!);
      }
   }
}
