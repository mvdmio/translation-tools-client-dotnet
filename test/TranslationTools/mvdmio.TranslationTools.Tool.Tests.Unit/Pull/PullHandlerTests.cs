using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerTests
{
   [Fact]
   public void ResolveRequest_ShouldResolveProjectDirectoryFromConfig()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project><PropertyGroup><RootNamespace>Demo.App</RootNamespace></PropertyGroup></Project>");

         var request = PullHandler.ResolveRequest(
            new ToolConfiguration
            {
               ConfigDirectory = projectDirectory,
               ApiKey = "api-key",
               DefaultLocale = "en"
            }
         );

         request.Should().NotBeNull();
         request!.ProjectDirectory.Should().Be(projectDirectory);
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public void BuildResxFiles_ShouldMapApiKeysBackToProjectFiles()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Shared.Validation.nl.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="required.message" xml:space="preserve">
                <value>Oud</value>
              </data>
            </root>
            """
         );

         var handler = new PullHandler();
         var files = handler.BuildResxFiles(
            projectDirectory,
            "en",
            new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal)
            {
               ["en"] = [
                  new TranslationItemResponse { Key = "Errors.title", Value = "Error" },
                  new TranslationItemResponse { Key = "Admin.Labels.save.button", Value = "Save" }
               ],
               ["nl"] = [
                  new TranslationItemResponse { Key = "Errors.title", Value = "Fout" },
                  new TranslationItemResponse { Key = "Shared.Validation.required.message", Value = "Verplicht" }
                ]
            },
            prune: false
         );

         files.Should().ContainSingle(x => x.FilePath.EndsWith($"Errors.resx", StringComparison.Ordinal));
         files.Should().ContainSingle(x => x.FilePath.EndsWith($"Admin{Path.DirectorySeparatorChar}Labels.resx", StringComparison.Ordinal));
         files.Should().ContainSingle(x => x.FilePath.EndsWith($"Errors.nl.resx", StringComparison.Ordinal));
         files.Should().ContainSingle(x => x.FilePath.EndsWith($"Shared.Validation.nl.resx", StringComparison.Ordinal));
         files.Single(x => x.FilePath.EndsWith("Shared.Validation.nl.resx", StringComparison.Ordinal)).Entries.Should().ContainSingle(x => x.Key == "required.message" && x.Value == "Verplicht");
         files.Single(x => x.FilePath.EndsWith("Errors.resx", StringComparison.Ordinal)).Entries.Should().ContainSingle(x => x.Key == "title" && x.Value == "Error");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public void BuildResxFiles_ShouldMapNormalizedLegacyApiKeysBackToExistingProjectFiles()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Button.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="editStreetSegments" xml:space="preserve">
                <value>Old</value>
              </data>
            </root>
            """
         );

         var handler = new PullHandler();
         var files = handler.BuildResxFiles(
            projectDirectory,
            "en",
            new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal)
            {
               ["en"] = [
                  new TranslationItemResponse { Key = "Button_EditStreetSegments", Value = "Edit street segments" }
               ]
            },
            prune: false
         );

         files.Should().ContainSingle(x => x.FilePath.EndsWith("Button.resx", StringComparison.Ordinal));
         files.Single().Entries.Should().ContainSingle(x => x.Key == "editStreetSegments" && x.Value == "Edit street segments");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public void BuildResxFiles_ShouldWriteSingleLocalizationsFile_WhenProjectHasNoExistingResxFiles()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         var handler = new PullHandler();
         var files = handler.BuildResxFiles(
            projectDirectory,
            "en",
            new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal)
            {
               ["en"] = [
                  new TranslationItemResponse { Key = "Action.save", Value = "Save" },
                  new TranslationItemResponse { Key = "Label.Street.name", Value = "Street" }
               ],
               ["nl"] = [
                  new TranslationItemResponse { Key = "Action.save", Value = "Opslaan" },
                  new TranslationItemResponse { Key = "Label.Street.name", Value = "Straat" }
               ]
            },
            prune: false
         );

         files.Should().ContainSingle(x => x.FilePath.EndsWith("Localizations.resx", StringComparison.Ordinal));
         files.Should().ContainSingle(x => x.FilePath.EndsWith("Localizations.nl.resx", StringComparison.Ordinal));
         files.Single(x => x.FilePath.EndsWith("Localizations.resx", StringComparison.Ordinal)).Entries.Should().BeEquivalentTo(
            [
               new { Key = "Action.save", Value = "Save" },
               new { Key = "Label.Street.name", Value = "Street" }
            ],
            options => options.ExcludingMissingMembers()
         );
         files.Single(x => x.FilePath.EndsWith("Localizations.nl.resx", StringComparison.Ordinal)).Entries.Should().BeEquivalentTo(
            [
               new { Key = "Action.save", Value = "Opslaan" },
               new { Key = "Label.Street.name", Value = "Straat" }
            ],
            options => options.ExcludingMissingMembers()
         );
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public void BuildResxFiles_ShouldKeepLegacyNormalizedApiKeysInSingleExistingResourceSet()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Translations.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="existing" xml:space="preserve">
                <value>Old</value>
              </data>
            </root>
            """
         );

         var handler = new PullHandler();
         var files = handler.BuildResxFiles(
            projectDirectory,
            "en",
            new Dictionary<string, TranslationItemResponse[]>(StringComparer.Ordinal)
            {
               ["en"] = [
                  new TranslationItemResponse { Key = "Button_EditStreetSegments", Value = "Edit street segments" }
               ]
            },
            prune: false
         );

         files.Should().ContainSingle(x => x.FilePath.EndsWith("Translations.resx", StringComparison.Ordinal));
         files.Single().Entries.Should().Contain(x => x.Key == "Button_EditStreetSegments" && x.Value == "Edit street segments");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }
}
