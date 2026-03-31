using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Client.SourceGenerator;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class TranslationManifestGeneratorTests
{
   [Fact]
   public void ShouldGenerateKeysAndProperties()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         namespace Demo;

         [Translations(KeyNaming = TranslationKeyNaming.UnderscoreToDot)]
         public static partial class Localizations
         {
            [Translation(DefaultValue = "Hello")]
            public static partial string Button_Hello { get; }

            public static partial string Button_Save { get; }

            [Translation(Key = "button.save_and_close", DefaultValue = "Save and close")]
            public static partial string Button_SaveAndClose { get; }
         }
         """
      );

       result.GeneratorDiagnostics.Should().BeEmpty();
       result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
       result.GeneratedSource.Should().Contain("private static global::System.Type ManifestType => typeof(Localizations);");
       result.GeneratedSource.Should().Contain("public const string Button_Hello = \"Button.Hello\";");
       result.GeneratedSource.Should().Contain("public const string Button_Save = \"Button.Save\";");
       result.GeneratedSource.Should().Contain("public const string Button_SaveAndClose = \"button.save_and_close\";");
      result.GeneratedSource.Should().Contain("public static partial string Button_Hello");
      result.GeneratedSource.Should().Contain("get => Get(Keys.Button_Hello, \"Hello\");");
      result.GeneratedSource.Should().Contain("public static partial string Button_Save");
      result.GeneratedSource.Should().Contain("get => Get(Keys.Button_Save);");
       result.GeneratedSource.Should().Contain("public static partial string Button_SaveAndClose");
       result.GeneratedSource.Should().Contain("get => Get(Keys.Button_SaveAndClose, \"Save and close\");");
       result.GeneratedSource.Should().Contain("public static global::System.Threading.Tasks.Task<string> GetAsync(string key");
    }

   [Fact]
   public void ShouldGenerateForNonStaticPartialClass()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         namespace Demo;

         [Translations]
         public partial class Localizations
         {
            public static partial string Button_Save { get; }
         }
         """
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.CompilationDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
      result.GeneratedSource.Should().Contain("public partial class Localizations");
      result.GeneratedSource.Should().Contain("public static partial string Button_Save");
      result.GeneratedSource.Should().Contain("get => Get(Keys.Button_Save);");
   }

   [Fact]
   public void ShouldUseLowerSnakeCaseByDefault()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         [Translations]
         public static partial class Localizations
         {
            public static partial string Button_HelloWorld { get; }
         }
         """
      );

       result.GeneratorDiagnostics.Should().BeEmpty();
       result.GeneratedSource.Should().Contain("public const string Button_HelloWorld = \"button_hello_world\";");
    }

   [Fact]
   public void ShouldReportMissingManifest()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         [Translations]
         public static partial class Localizations
         {
          }
         """
      );

      result.GeneratorDiagnostics.Select(x => x.Id).Should().Contain("TTCLIENTGEN002");
   }

   [Fact]
   public void ShouldReportInvalidManifestProperty()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         [Translations]
         public static partial class Localizations
         {
            public static string Button_Hello { get; set; }
          }
         """
      );

      result.GeneratorDiagnostics.Select(x => x.Id).Should().Contain("TTCLIENTGEN003");
   }

   [Fact]
   public void ShouldUseCultureOverrideWhenClassDefinesCultureProperty()
   {
      var result = RunGenerator(
         """
         using System.Globalization;
         using mvdmio.TranslationTools.Client;

         [Translations]
         public static partial class Localizations
         {
            public static CultureInfo? Culture { get; set; }

            [Translation(DefaultValue = "Hello")]
            public static partial string Button_Hello { get; }
         }
         """
      );

       result.GeneratorDiagnostics.Should().BeEmpty();
       result.GeneratedSource.Should().Contain("TranslationManifestRuntime.Get(ManifestType, key, Culture ?? global::System.Globalization.CultureInfo.CurrentUICulture, defaultValue)");
    }

   [Fact]
   public void ShouldGenerateMultiplePartialProperties()
   {
      var result = RunGenerator(
         """
         using mvdmio.TranslationTools.Client;

         [Translations]
         public partial class Localizations
         {
            public static partial string Action_Save { get; }
            public static partial string Label_Name { get; }
         }
         """
      );

      result.GeneratorDiagnostics.Should().BeEmpty();
      result.GeneratedSource.Should().Contain("public const string Action_Save = \"action_save\";");
      result.GeneratedSource.Should().Contain("public const string Label_Name = \"label_name\";");
   }

   private static GeneratorTestResult RunGenerator(string source)
   {
      var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
      var compilation = CSharpCompilation.Create(
         assemblyName: "GeneratorTests",
         syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
         references: GetReferences(),
         options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
      );

      GeneratorDriver driver = CSharpGeneratorDriver.Create(
         generators: [new TranslationManifestGenerator().AsSourceGenerator()],
         parseOptions: parseOptions
      );

      driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var outputDiagnostics);
      var runResult = driver.GetRunResult();
      var generatedSource = runResult.Results.Single().GeneratedSources.SingleOrDefault().SourceText?.ToString() ?? string.Empty;

      return new GeneratorTestResult(
         GeneratedSource: generatedSource,
         GeneratorDiagnostics: runResult.Results.SelectMany(x => x.Diagnostics).ToImmutableArray(),
         CompilationDiagnostics: [.. outputCompilation.GetDiagnostics(), .. outputDiagnostics]
      );
   }

   private static MetadataReference[] GetReferences()
   {
      var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
         .Split(Path.PathSeparator)
         .Select(path => MetadataReference.CreateFromFile(path))
         .ToList();
      trustedPlatformAssemblies.Add(MetadataReference.CreateFromFile(typeof(TranslationsAttribute).Assembly.Location));
      return [.. trustedPlatformAssemblies.DistinctBy(x => x.Display, StringComparer.OrdinalIgnoreCase)];
   }

   private sealed record GeneratorTestResult(
      string GeneratedSource,
      ImmutableArray<Diagnostic> GeneratorDiagnostics,
      ImmutableArray<Diagnostic> CompilationDiagnostics
   );
}
