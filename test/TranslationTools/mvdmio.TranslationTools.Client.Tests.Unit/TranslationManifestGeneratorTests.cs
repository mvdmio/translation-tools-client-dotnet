using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using mvdmio.TranslationTools.Client.SourceGenerator;
using System.Reflection;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationManifestGeneratorTests
{
   [Fact]
   public void ShouldGenerateFromNeutralResxFile()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Button.Hello", "Hello"), ("Button.Save", null)))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
      result.GeneratedSource.Should().Contain("public static partial class Localizations");
      result.GeneratedSource.Should().Contain("private const string Origin = \"/Localizations.resx\";");
      result.GeneratedSource.Should().Contain("public static readonly global::mvdmio.TranslationTools.Client.TranslationRef Button_Hello = new(Origin, \"Button.Hello\");");
      result.GeneratedSource.Should().Contain("public static readonly global::mvdmio.TranslationTools.Client.TranslationRef Button_Save = new(Origin, \"Button.Save\");");
      result.GeneratedSource.Should().Contain("get => Get(\"Button.Hello\", \"Hello\");");
      result.GeneratedSource.Should().Contain("get => Get(\"Button.Save\");");
   }

   [Fact]
   public void ShouldIgnoreLocalizedResxVariants()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Button.Save", "Save"))),
            ("src/Demo/Localizations.nl.resx", Resx(("Button.Save", "Opslaan")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSource.Should().Contain("Button_Save");
      result.GeneratedSource.Should().NotContain("Opslaan");
   }

   [Fact]
   public void ShouldSkipEmptyResxFile()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Empty.resx", Resx())
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSource.Should().BeEmpty();
   }

   [Fact]
   public void ShouldGenerateDistinctSanitizedPropertyNames()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Action.Save", "Save"), ("Action-Save", "Save alt"), ("123.Start", "Start")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSource.Should().Contain("Action_Save");
      result.GeneratedSource.Should().Contain("_123_Start");
   }

   [Fact]
   public void ShouldReferenceStableRoslynAssemblies()
   {
      var references = typeof(TranslationManifestGenerator).Assembly.GetReferencedAssemblies();

      references.Should().Contain(x => x.Name == "Microsoft.CodeAnalysis");
   }

   private static GeneratorTestResult RunGenerator(string source, IReadOnlyCollection<(string Path, string Content)> additionalFiles)
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
         generators: [new TranslationManifestGenerator().AsSourceGenerator()],
         additionalTexts: additionalFiles.Select(static file => new TestAdditionalText(file.Path, file.Content)).ToImmutableArray<AdditionalText>(),
         parseOptions: parseOptions
      );

      driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var outputDiagnostics);
      var runResult = driver.GetRunResult();
      var generatedSource = string.Join(
         Environment.NewLine,
         runResult.Results.SelectMany(static x => x.GeneratedSources).Select(static x => x.SourceText.ToString())
      );

      return new GeneratorTestResult(
         GeneratedSources: [.. runResult.Results.SelectMany(static x => x.GeneratedSources).Select(static x => x.SourceText.ToString())],
         GeneratorDiagnostics: runResult.Results.SelectMany(static x => x.Diagnostics).ToImmutableArray(),
         CompilationDiagnostics: [.. outputCompilation.GetDiagnostics(), .. outputDiagnostics]
      );
   }

   private static MetadataReference[] GetReferences()
   {
      return ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
         .Split(Path.PathSeparator)
         .Select(path => MetadataReference.CreateFromFile(path))
         .DistinctBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
         .ToArray();
   }

   private static string Resx(params (string Key, string? Value)[] entries)
   {
      var data = string.Join(Environment.NewLine, entries.Select(x => x.Value is null
         ? $"  <data name=\"{x.Key}\"><value></value></data>"
         : $"  <data name=\"{x.Key}\"><value>{System.Security.SecurityElement.Escape(x.Value)}</value></data>"));
      return $$"""
               <?xml version="1.0" encoding="utf-8"?>
               <root>
               {{data}}
               </root>
               """;
   }

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

   private sealed record GeneratorTestResult(
      ImmutableArray<string> GeneratedSources,
      ImmutableArray<Diagnostic> GeneratorDiagnostics,
      ImmutableArray<Diagnostic> CompilationDiagnostics
   )
   {
      public string GeneratedSource => string.Join(Environment.NewLine, GeneratedSources);
   }
}
