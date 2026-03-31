using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Migrate;
using mvdmio.TranslationTools.Tool.Pull;
using mvdmio.TranslationTools.Tool.Scaffolding;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Migrate;

public class MigrateHandlerTests
{
   public class ResxMigrationScannerTests
   {
      [Fact]
      public void ShouldDetectMultipleLogicalResxSetsInOneProject()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), EmptyResx());
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Admin"));
            File.WriteAllText(Path.Combine(projectDirectory, "Admin", "Labels.nl.resx"), EmptyResx());

            var result = new ResxMigrationScanner().ScanProject(projectDirectory);

            result.SourceFiles.Should().HaveCount(2);
            result.SourceFiles.Should().ContainSingle(x => x.ResourceSetName == "Errors" && x.Locale == null);
            result.SourceFiles.Should().ContainSingle(x => x.ResourceSetName == "Admin.Labels" && x.Locale == "nl");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldFailWhenNoResxLocaleFilesAreFound()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            var action = () => new ResxMigrationScanner().ScanProject(projectDirectory);

            action.Should().Throw<InvalidOperationException>().WithMessage("*No .resx locale files found*");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldFailWhenMultipleResxFilesNormalizeToSameLocale()
      {
         var exception = Assert.Throws<InvalidOperationException>(
            () => ResxMigrationScanner.ValidateSourceFiles(
               [
                  new ResxMigrationSourceFile {
                     FilePath = "Errors.nl.resx",
                     RelativePath = "Errors.nl.resx",
                     ResourceSetPath = "Errors",
                     ResourceSetName = "Errors",
                     Locale = "nl"
                  },
                  new ResxMigrationSourceFile {
                     FilePath = "Errors.NL.resx",
                     RelativePath = "Errors.NL.resx",
                     ResourceSetPath = "Errors",
                     ResourceSetName = "Errors",
                     Locale = "nl"
                  }
               ]
            )
         );

         exception.Message.Should().Contain("same locale");
      }
   }

   public class ResxResourceSetParserTests
   {
      [Fact]
      public void ShouldParseLocalizedVariantsIntoExpectedImportModel()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            var path = Path.Combine(projectDirectory, "Shared.Validation.nl.resx");
            File.WriteAllText(path, Resx(("Required", "Verplicht"), ("Optional", "Optioneel")));

            var result = new ResxResourceSetParser().Parse(
               new ResxMigrationSourceFile {
                  FilePath = path,
                  RelativePath = "Shared.Validation.nl.resx",
                  ResourceSetPath = "Shared.Validation",
                  ResourceSetName = "Shared.Validation",
                  Locale = "nl"
               }
            );

            result.Entries.Should().ContainSingle(x => x.Key == "Required" && x.Value == "Verplicht");
            result.Entries.Should().ContainSingle(x => x.Key == "Optional" && x.Value == "Optioneel");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldFailOnDuplicateKeysInOneLocale()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            var path = Path.Combine(projectDirectory, "Errors.resx");
            File.WriteAllText(path, Resx(("Title", "One"), ("Title", "Two")));

            var action = () => new ResxResourceSetParser().Parse(
               new ResxMigrationSourceFile {
                  FilePath = path,
                  RelativePath = "Errors.resx",
                  ResourceSetPath = "Errors",
                  ResourceSetName = "Errors",
                  Locale = null
               }
            );

            action.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate keys*");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }
   }

   public class ProjectTranslationStateBuilderTests
   {
      [Fact]
      public void ShouldImportLocalizedOnlyKeysEvenWhenMissingFromBaseFile()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), Resx(("Title", "Error")));
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.nl.resx"), Resx(("Title", "Fout"), ("OnlyNl", "Alleen nl")));

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Items.Should().ContainSingle(x => x.Key == "OnlyNl" && x.Translations["en"] == null && x.Translations["nl"] == "Alleen nl");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldKeepEmptyResxLocaleFilesInImportedLocaleMetadataAndReportWarning()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), Resx(("Title", "Error")));
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.nl.resx"), EmptyResx());

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Locales.Should().Equal("en", "nl");
            result.Report.Warnings.Should().ContainSingle(x => x.Contains("Errors.nl.resx", StringComparison.Ordinal));
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldAllowMigrationWhenBaseDefaultResxFileIsEmpty()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), EmptyResx());
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.nl.resx"), Resx(("Title", "Fout")));

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Items.Should().ContainSingle(x => x.Key == "Title" && x.Translations["en"] == null && x.Translations["nl"] == "Fout");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldContinueWithWarningWhenAllResxLocaleFilesAreEmpty()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), EmptyResx());
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.nl.resx"), EmptyResx());

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Items.Should().BeEmpty();
            result.State.Locales.Should().Equal("en", "nl");
            result.Report.Warnings.Should().HaveCount(2);
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldUseOriginalKeysWhenProjectHasSingleLogicalResourceSet()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Admin"));
            File.WriteAllText(Path.Combine(projectDirectory, "Admin", "Labels.resx"), Resx(("Title", "Admin title")));
            File.WriteAllText(Path.Combine(projectDirectory, "Admin", "Labels.nl.resx"), Resx(("Title", "Beheer titel")));

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Items.Should().ContainSingle(x => x.Key == "Title");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldPrefixApiKeysWhenProjectHasMultipleLogicalResourceSets()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Admin"));
            File.WriteAllText(Path.Combine(projectDirectory, "Admin", "Labels.resx"), Resx(("Title", "Admin title")));
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), Resx(("Title", "Error title")));

            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);
            var result = new ProjectTranslationStateBuilder().Build(scanResult, "en");

            result.State.Items.Should().Contain(x => x.Key == "Admin.Labels.Title");
            result.State.Items.Should().Contain(x => x.Key == "Errors.Title");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldFailIfChosenPrefixingStrategyStillProducesDuplicateEffectiveApiKeys()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Shared.Validation.resx"), Resx(("Required", "A")));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Shared"));
            File.WriteAllText(Path.Combine(projectDirectory, "Shared", "Validation.resx"), Resx(("Required", "B")));

            var action = () => new ResxMigrationScanner().ScanProject(projectDirectory);

            action.Should().Throw<InvalidOperationException>().WithMessage("*prefix collision*");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }

      [Fact]
      public void ShouldRequireDefaultLocaleWhenBaseFileLocaleCannotBeInferred()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), Resx(("Title", "Error")));
            var scanResult = new ResxMigrationScanner().ScanProject(projectDirectory);

            var action = () => new ProjectTranslationStateBuilder().Build(scanResult, defaultLocale: null);

            action.Should().Throw<InvalidOperationException>().WithMessage("*default locale*");
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }
   }

   public class HandleAsyncTests
   {
      [Fact]
      public async Task ShouldFailWhenConfigIsMissingAndInstructUserToRunInit()
      {
         var currentDirectory = Directory.GetCurrentDirectory();
         var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
         Directory.CreateDirectory(tempDirectory);

         try
         {
            Directory.SetCurrentDirectory(tempDirectory);
            var reporter = new TestMigrateReporter();

            await new MigrateHandler(new MissingConfigurationLoader(), new FakeTranslationApiService(), new ResxMigrationScanner(), new ProjectTranslationStateBuilder(), new FakeMigratePullRunner(), reporter).HandleAsync(TestContext.Current.CancellationToken);

            reporter.Errors.Should().ContainSingle(x => x.Contains("translations init", StringComparison.Ordinal));
         }
         finally
         {
            Directory.SetCurrentDirectory(currentDirectory);
            Directory.Delete(tempDirectory, recursive: true);
         }
      }

      [Fact]
      public async Task ShouldWriteConfigAndThenReusePullFlow()
      {
         var projectDirectory = CreateTempProject();

         try
         {
            File.WriteAllText(Path.Combine(projectDirectory, "Errors.resx"), Resx(("Title", "Error")));

            var apiService = new FakeTranslationApiService {
               Metadata = new ProjectMetadataResponse {
                  Locales = ["en"],
                  DefaultLocale = "en"
               }
            };
            var pullRunner = new FakeMigratePullRunner();
            var reporter = new TestMigrateReporter();

            var handler = new MigrateHandler(new MissingConfigurationLoader(), apiService, new ResxMigrationScanner(), new ProjectTranslationStateBuilder(), pullRunner, reporter);

            await handler.HandleAsync(
               new ToolConfiguration {
                  ConfigDirectory = projectDirectory,
                  ApiKey = "api-key",
                  Output = "Localizations.cs"
               },
               TestContext.Current.CancellationToken
            );

            apiService.ImportRequest.Should().NotBeNull();
            apiService.ImportRequest!.DefaultLocale.Should().Be("en");
            apiService.ImportRequest.Items.Should().ContainSingle(x => x.Key == "Title");
            pullRunner.Calls.Should().ContainSingle();
            pullRunner.Calls[0].overwrite.Should().BeTrue();
         }
         finally
         {
            Directory.Delete(projectDirectory, recursive: true);
         }
      }
   }

   private static string CreateTempProject()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);
      File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
      return projectDirectory;
   }

   private static string EmptyResx()
   {
      return """
             <?xml version="1.0" encoding="utf-8"?>
             <root />
             """;
   }

   private static string Resx(params (string Key, string? Value)[] entries)
   {
      var data = string.Join(Environment.NewLine, entries.Select(x => $"  <data name=\"{x.Key}\"><value>{System.Security.SecurityElement.Escape(x.Value)}</value></data>"));
      return $$"""
               <?xml version="1.0" encoding="utf-8"?>
               <root>
               {{data}}
               </root>
               """;
   }

   private sealed class FakeTranslationApiService : ITranslationApiService
   {
      public ProjectMetadataResponse Metadata { get; set; } = new() {
         Locales = ["en"],
         DefaultLocale = "en"
      };

      public ProjectTranslationStateImportRequest? ImportRequest { get; private set; }

      public Task<ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken)
      {
         return Task.FromResult(Metadata);
      }

      public Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
      {
         return Task.FromResult(Array.Empty<TranslationItemResponse>());
      }

      public Task<mvdmio.TranslationTools.Tool.Push.TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, mvdmio.TranslationTools.Tool.Push.TranslationPushRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }

      public Task<ProjectTranslationStateImportResponse> ImportProjectStateAsync(string apiKey, ProjectTranslationStateImportRequest request, CancellationToken cancellationToken)
      {
         ImportRequest = request;
         return Task.FromResult(
            new ProjectTranslationStateImportResponse {
               ReceivedKeyCount = request.Items.Length,
               ReceivedLocaleCount = request.Locales.Length,
               CreatedTranslationCount = request.Items.Sum(x => x.Translations.Count),
               UpdatedTranslationCount = 0
            }
         );
      }
   }

   private sealed class MissingConfigurationLoader : IMigrateConfigurationLoader
   {
      public bool TryLoad(out ToolConfiguration config)
      {
         config = new ToolConfiguration();
         return false;
      }
   }

   private sealed class FakeMigratePullRunner : IMigratePullRunner
   {
      public List<(ToolConfiguration config, bool overwrite)> Calls { get; } = [];

      public Task RunAsync(ToolConfiguration config, bool overwrite, CancellationToken cancellationToken)
      {
         Calls.Add((config, overwrite));
         return Task.CompletedTask;
      }
   }

   private sealed class TestMigrateReporter : IMigrateReporter
   {
      public List<string> Infos { get; } = [];
      public List<string> Warnings { get; } = [];
      public List<string> Errors { get; } = [];

      public void WriteInfo(string message)
      {
         Infos.Add(message);
      }

      public void WriteWarning(string message)
      {
         Warnings.Add(message);
      }

      public void WriteError(string message)
      {
         Errors.Add(message);
      }
   }
}
