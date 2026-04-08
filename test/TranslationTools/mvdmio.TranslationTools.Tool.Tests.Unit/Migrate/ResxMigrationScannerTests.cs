using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Migrate;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Migrate;

public class ResxMigrationScannerTests
{
   [Fact]
   public void ScanProject_ShouldIgnoreBinAndObjAndNormalizeLocales()
   {
      var projectDirectory = CreateProjectDirectory();

      try
      {
         WriteResx(Path.Combine(projectDirectory, "Resources", "Messages.EN-US.resx"));
         WriteResx(Path.Combine(projectDirectory, "Resources", "Messages.resx"));
         WriteResx(Path.Combine(projectDirectory, "bin", "Debug", "Ignored.resx"));
         WriteResx(Path.Combine(projectDirectory, "obj", "Release", "IgnoredToo.resx"));

         var result = new ResxMigrationScanner().ScanProject(projectDirectory);

         result.HasBaseFiles.Should().BeTrue();
         result.SourceFiles.Should().HaveCount(2);
         result.SourceFiles.Select(static x => x.RelativePath).Should().Equal(
            Path.Combine("Resources", "Messages.EN-US.resx"),
            Path.Combine("Resources", "Messages.resx")
         );
         result.SourceFiles.Single(static x => x.Locale is not null).Locale.Should().Be("en-us");
      }
      finally
      {
         DeleteDirectory(projectDirectory);
      }
   }

   [Fact]
   public void ValidateSourceFiles_ShouldThrow_WhenResourceSetNamesCollideAcrossDirectories()
   {
      var act = () => ResxMigrationScanner.ValidateSourceFiles(
         [
            CreateSourceFile("Feature\\Errors.resx", "Feature\\Errors", "Feature.Errors", null),
            CreateSourceFile("Feature.Errors.resx", "Feature.Errors", "Feature.Errors", null)
         ]
      );

      act.Should().Throw<InvalidOperationException>().WithMessage("*Resource-set prefix collision detected*");
   }

   [Fact]
   public void ValidateSourceFiles_ShouldThrow_WhenNormalizedLocaleDuplicatesExist()
   {
      var act = () => ResxMigrationScanner.ValidateSourceFiles(
         [
            CreateSourceFile("Messages.en-US.resx", "Messages", "Messages", "en-us"),
            CreateSourceFile("Messages.en-us.resx", "Messages", "Messages", "en-us")
         ]
      );

      act.Should().Throw<InvalidOperationException>().WithMessage("*normalize to the same locale*resource set 'Messages'*");
   }

   [Theory]
   [InlineData(" EN ", "en")]
   [InlineData("nl-NL", "nl-nl")]
   public void NormalizeLocale_ShouldTrimAndLowercase(string input, string expected)
   {
      ResxMigrationScanner.NormalizeLocale(input).Should().Be(expected);
   }

   private static ResxMigrationSourceFile CreateSourceFile(string relativePath, string resourceSetPath, string resourceSetName, string? locale)
   {
      return new ResxMigrationSourceFile
      {
         FilePath = relativePath,
         RelativePath = relativePath,
         ResourceSetPath = resourceSetPath,
         ResourceSetName = resourceSetName,
         Locale = locale
      };
   }

   private static string CreateProjectDirectory()
   {
      var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(path);
      return path;
   }

   private static void WriteResx(string path)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, "<root />");
   }

   private static void DeleteDirectory(string path)
   {
      if (Directory.Exists(path))
         Directory.Delete(path, recursive: true);
   }
}
