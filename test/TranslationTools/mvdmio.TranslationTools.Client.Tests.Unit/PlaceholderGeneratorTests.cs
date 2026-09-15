using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using mvdmio.TranslationTools.Client.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class PlaceholderGeneratorTests
{
   [Fact]
   public void KeyWithTokens_EmitsMethodWithRequiredStringParameters()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Greeting", "Hi {userName}, you have {orderCount} orders")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

      // Method (not property) with one required string param per token, first-seen order.
      result.GeneratedSource.Should().Contain("public static string Greeting(string userName, string orderCount)");
      result.GeneratedSource.Should().Contain("[\"userName\"] = userName");
      result.GeneratedSource.Should().Contain("[\"orderCount\"] = orderCount");
      result.GeneratedSource.Should().Contain("GetWithPlaceholders");

      // Verify the emitted public API surface compiles into a method with the expected signature.
      var method = GetGeneratedMethod(result, "Greeting");
      method.Should().NotBeNull();
      method!.Parameters.Select(p => p.Name).Should().Equal("userName", "orderCount");
      method.Parameters.Select(p => p.Type.SpecialType).Should().AllSatisfy(t => t.Should().Be(SpecialType.System_String));
      method.Parameters.Should().AllSatisfy(p => p.HasExplicitDefaultValue.Should().BeFalse());
   }

   [Fact]
   public void KeyWithoutTokens_KeepsPropertyForm()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Plain", "No tokens here")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSource.Should().Contain("public static string Plain");
      result.GeneratedSource.Should().Contain("get => Get(\"Plain\", \"No tokens here\");");
   }

   [Fact]
   public void TokenMatchingDeclaredGlobal_IsExcludedFromParameters()
   {
      var source = """
         using mvdmio.TranslationTools.Client;
         namespace Demo;
         public sealed class Globals
         {
            [GlobalPlaceholder]
            public string CompanyName => "Acme";
         }
         """;

      var result = RunGenerator(
         source: source,
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Welcome", "Welcome to {companyName}, {userName}")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

      // companyName is a declared global -> excluded from params; userName remains required.
      result.GeneratedSource.Should().Contain("public static string Welcome(string userName)");
      result.GeneratedSource.Should().NotContain("string companyName");

      var method = GetGeneratedMethod(result, "Welcome");
      method!.Parameters.Select(p => p.Name).Should().Equal("userName");

      // knownSet includes both the key-scoped token and the declared global name.
      result.GeneratedSource.Should().Contain("\"userName\"");
      result.GeneratedSource.Should().Contain("\"companyName\"");
   }

   [Fact]
   public void KeywordToken_EscapedAsParameterIdentifier()
   {
      var result = RunGenerator(
         source: "namespace Demo; public sealed class Marker;",
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Choice", "Pick {class}")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

      // 'class' is a C# keyword -> escaped with @ in the parameter list; binding key stays raw.
      result.GeneratedSource.Should().Contain("public static string Choice(string @class)");
      result.GeneratedSource.Should().Contain("[\"class\"] = @class");

      var method = GetGeneratedMethod(result, "Choice");
      method!.Parameters.Select(p => p.Name).Should().Equal("class");
   }

   [Fact]
   public void DeclaredGlobalNameOverride_UsedForExclusion()
   {
      var source = """
         using mvdmio.TranslationTools.Client;
         namespace Demo;
         public sealed class Globals
         {
            [GlobalPlaceholder("brand")]
            public string TheBrand => "Acme";
         }
         """;

      var result = RunGenerator(
         source: source,
         additionalFiles: [
            ("src/Demo/Localizations.resx", Resx(("Tag", "By {brand}")))
         ]
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

      // brand is a declared global (via override) -> excluded from parameters...
      result.GeneratedSource.Should().NotContain("string brand");
      // ...but the value still has a token, so a no-arg method runs substitution (globals resolve at runtime).
      result.GeneratedSource.Should().Contain("public static string Tag()");
      result.GeneratedSource.Should().Contain("GetWithPlaceholders");
      result.GeneratedSource.Should().Contain("\"brand\"");

      var method = GetGeneratedMethod(result, "Tag");
      method!.Parameters.Should().BeEmpty();
   }

   private static IMethodSymbol? GetGeneratedMethod(GeneratorTestResult result, string methodName)
   {
      var compilation = result.OutputCompilation;
      var type = compilation.GetTypeByMetadataName("GeneratorTests.src.Demo.Localizations");
      if (type is null)
         return null;

      return type.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
   }

   private static GeneratorTestResult RunGenerator(
      string source,
      IReadOnlyCollection<(string Path, string Content)> additionalFiles,
      IReadOnlyDictionary<string, string>? globalOptions = null)
   {
      var runtimeStub = """
          namespace mvdmio.TranslationTools.Client
          {
             public readonly record struct TranslationRef(string Origin, string Key);

             [System.AttributeUsage(System.AttributeTargets.Property)]
             public sealed class GlobalPlaceholderAttribute : System.Attribute
             {
                public GlobalPlaceholderAttribute(string? name = null) { Name = name; }
                public string? Name { get; }
             }

             public sealed class TranslationCatalogKey
             {
                public TranslationCatalogKey(string origin, string key, string? neutralValue = null, System.Collections.Generic.IReadOnlyDictionary<string, string>? localeValues = null)
                {
                   Origin = origin;
                   Key = key;
                   NeutralValue = neutralValue;
                   LocaleValues = localeValues ?? new System.Collections.Generic.Dictionary<string, string>();
                }

                public string Origin { get; }
                public string Key { get; }
                public string? NeutralValue { get; }
                public System.Collections.Generic.IReadOnlyDictionary<string, string> LocaleValues { get; }
             }

             public static class TranslationCatalog
             {
                public static void Register(params TranslationCatalogKey[] keys) { }
                public static void Register(System.Collections.Generic.IEnumerable<TranslationCatalogKey> keys) { }
             }

             public static class Translations
             {
                public static string Get(TranslationRef translation, string? defaultValue = null) => defaultValue ?? translation.Key;
                public static string Get(TranslationRef translation, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues) => defaultValue ?? translation.Key;
                public static string Get(TranslationRef translation, System.Globalization.CultureInfo locale, string? defaultValue = null) => defaultValue ?? translation.Key;
                public static string Get(TranslationRef translation, System.Globalization.CultureInfo locale, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues) => defaultValue ?? translation.Key;

                public static System.Threading.Tasks.Task<string> GetAsync(TranslationRef translation, string? defaultValue = null, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(defaultValue ?? translation.Key);
                public static System.Threading.Tasks.Task<string> GetAsync(TranslationRef translation, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(defaultValue ?? translation.Key);
                public static System.Threading.Tasks.Task<string> GetAsync(TranslationRef translation, System.Globalization.CultureInfo locale, string? defaultValue = null, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(defaultValue ?? translation.Key);
                public static System.Threading.Tasks.Task<string> GetAsync(TranslationRef translation, System.Globalization.CultureInfo locale, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(defaultValue ?? translation.Key);

                public static string GetWithPlaceholders(TranslationRef translation, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues, System.Collections.Generic.IReadOnlyDictionary<string, string?>? bindings, System.Collections.Generic.IReadOnlyCollection<string>? knownSet)
                   => defaultValue ?? translation.Key;
                public static string GetWithPlaceholders(TranslationRef translation, System.Globalization.CultureInfo locale, string? defaultValue, System.Collections.Generic.IReadOnlyDictionary<string, string?>? localeValues, System.Collections.Generic.IReadOnlyDictionary<string, string?>? bindings, System.Collections.Generic.IReadOnlyCollection<string>? knownSet)
                   => defaultValue ?? translation.Key;
             }
          }
         """;

      var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
      var compilation = CSharpCompilation.Create(
         assemblyName: "GeneratorTests",
         syntaxTrees:
         [
            CSharpSyntaxTree.ParseText(runtimeStub, parseOptions),
            CSharpSyntaxTree.ParseText(source, parseOptions)
         ],
         references: GetReferences(),
         options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
      );

      var analyzerConfig = new TestAnalyzerConfigOptionsProvider(globalOptions ?? new Dictionary<string, string>(StringComparer.Ordinal)
      {
         ["build_property.MSBuildProjectDirectory"] = "D:\\Project",
         ["build_property.MSBuildProjectName"] = "GeneratorTests",
         ["build_property.RootNamespace"] = "GeneratorTests"
      });

      GeneratorDriver driver = CSharpGeneratorDriver.Create(
         generators: [new TranslationManifestGenerator().AsSourceGenerator()],
         additionalTexts: additionalFiles.Select(static file => new TestAdditionalText(file.Path, file.Content)).ToImmutableArray<AdditionalText>(),
         parseOptions: parseOptions,
         optionsProvider: analyzerConfig
      );

      driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var outputDiagnostics);
      var runResult = driver.GetRunResult();

      return new GeneratorTestResult(
         GeneratedSources: [.. runResult.Results.SelectMany(static x => x.GeneratedSources).Select(static x => x.SourceText.ToString())],
         GeneratorDiagnostics: runResult.Results.SelectMany(static x => x.Diagnostics).ToImmutableArray(),
         CompilationDiagnostics: [.. outputCompilation.GetDiagnostics(), .. outputDiagnostics],
         OutputCompilation: outputCompilation
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

      public override SourceText GetText(CancellationToken cancellationToken = default) => _sourceText;
   }

   private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
   {
      private readonly AnalyzerConfigOptions _globalOptions;

      public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
      {
         _globalOptions = new TestAnalyzerConfigOptions(values);
      }

      public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
      public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());
      public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());
   }

   private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
   {
      private readonly IReadOnlyDictionary<string, string> _values;

      public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
      {
         _values = values;
      }

      public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
   }

   private sealed record GeneratorTestResult(
      ImmutableArray<string> GeneratedSources,
      ImmutableArray<Diagnostic> GeneratorDiagnostics,
      ImmutableArray<Diagnostic> CompilationDiagnostics,
      Compilation OutputCompilation
   )
   {
      public string GeneratedSource => string.Join(Environment.NewLine, GeneratedSources);
   }
}
