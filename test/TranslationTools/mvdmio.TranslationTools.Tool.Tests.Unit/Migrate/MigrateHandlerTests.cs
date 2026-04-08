using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Migrate;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Migrate;

public class MigrateHandlerTests
{
   [Fact]
   public void TryGetSingleResourceSetPrefix_ShouldReturnSharedPrefix_WhenAllFilesShareOneResourceSet()
   {
      var result = MigrateHandler.TryGetSingleResourceSetPrefix(
         new ResxMigrationScanResult
         {
            HasBaseFiles = true,
            SourceFiles =
            [
               CreateSourceFile("Errors.resx", "Errors"),
               CreateSourceFile("Errors.nl.resx", "Errors", "nl")
            ]
         },
         out var sharedKeyPrefix
      );

      result.Should().BeTrue();
      sharedKeyPrefix.Should().Be("Errors");
   }

   [Fact]
   public void CreatePullConfig_ShouldCopySharedPrefix_WhenScannerFoundSingleResourceSet()
   {
      var config = new ToolConfiguration
      {
         ConfigDirectory = "C:\\repo",
         ApiKey = "api-key",
         DefaultLocale = "en",
         Output = "Generated\\Localizations.g.cs",
         Namespace = "Demo.Generated",
         ClassName = "Texts"
      };

      var pullConfig = MigrateHandler.CreatePullConfig(
         config,
         new ResxMigrationScanResult
         {
            HasBaseFiles = true,
            SourceFiles =
            [
               CreateSourceFile("Errors.resx", "Errors"),
               CreateSourceFile("Errors.nl.resx", "Errors", "nl")
            ]
         }
      );

      pullConfig.Should().NotBeSameAs(config);
      pullConfig.ConfigDirectory.Should().Be(config.ConfigDirectory);
      pullConfig.ApiKey.Should().Be(config.ApiKey);
      pullConfig.DefaultLocale.Should().Be(config.DefaultLocale);
      pullConfig.Output.Should().Be(config.Output);
      pullConfig.Namespace.Should().Be(config.Namespace);
      pullConfig.ClassName.Should().Be(config.ClassName);
      pullConfig.SharedKeyPrefix.Should().Be("Errors");
   }

   [Fact]
   public void CreatePullConfig_ShouldReturnOriginalConfig_WhenMultipleResourceSetsExist()
   {
      var config = new ToolConfiguration
      {
         ConfigDirectory = "C:\\repo",
         ApiKey = "api-key"
      };

      var pullConfig = MigrateHandler.CreatePullConfig(
         config,
         new ResxMigrationScanResult
         {
            HasBaseFiles = true,
            SourceFiles =
            [
               CreateSourceFile("Errors.resx", "Errors"),
               CreateSourceFile("Labels.resx", "Labels")
            ]
         }
      );

      pullConfig.Should().BeSameAs(config);
   }

   private static ResxMigrationSourceFile CreateSourceFile(string relativePath, string resourceSetName, string? locale = null)
   {
      return new ResxMigrationSourceFile
      {
         FilePath = relativePath,
         RelativePath = relativePath,
         ResourceSetPath = resourceSetName,
         ResourceSetName = resourceSetName,
         Locale = locale
      };
   }
}
