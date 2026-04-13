using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerLoggingTests
{
   [Fact]
   public async Task HandleAsync_ShouldLogMissingApiKey()
   {
      var reporter = new TestPullReporter();
      var handler = new PullHandler(new TranslationApiService(), new TestPullFileSystem(), reporter);

      await handler.HandleAsync(
         new ToolConfiguration
         {
            ApiKey = null,
            DefaultLocale = "en"
         },
         prune: false,
         TestContext.Current.CancellationToken
      );

      reporter.Errors.Should().ContainSingle("Error: No API key provided. Add apiKey to .mvdmio-translations.yml.");
   }

   [Fact]
   public async Task HandleAsync_ShouldLogMissingDefaultLocale()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);
      var reporter = new TestPullReporter();
      var handler = new PullHandler(new TranslationApiService(), new TestPullFileSystem(), reporter);

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");

         await handler.HandleAsync(
            new ToolConfiguration
            {
               ConfigDirectory = projectDirectory,
               ApiKey = "api-key",
               DefaultLocale = null
            },
            prune: false,
            TestContext.Current.CancellationToken
         );

         reporter.Errors.Should().ContainSingle("Error: No default locale provided. Add defaultLocale to .mvdmio-translations.yml.");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public async Task HandleAsync_ShouldLogPerLocaleChangeCounts()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);
      var reporter = new TestPullReporter();
      var fileSystem = new TestPullFileSystem();

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");

         fileSystem.Files[Path.Combine(projectDirectory, "Localizations.resx")] = """
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Button.Cancel" xml:space="preserve"><value>Cancel old</value></data>
  <data name="Button.Legacy" xml:space="preserve"><value>Legacy</value></data>
</root>
""";
         fileSystem.Files[Path.Combine(projectDirectory, "Localizations.nl.resx")] = """
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Button.Cancel" xml:space="preserve"><value>Annuleren oud</value></data>
  <data name="Button.Legacy" xml:space="preserve"><value>Verouderd</value></data>
</root>
""";

         var handler = new PullHandler(new LoggingTranslationApiService(), fileSystem, reporter);

         await handler.HandleAsync(
            new ToolConfiguration
            {
               ConfigDirectory = projectDirectory,
               ApiKey = "api-key",
               DefaultLocale = "en"
            },
            prune: false,
            TestContext.Current.CancellationToken
         );

         reporter.Infos.Should().Contain("Locale 'en': +1 ~1 -1");
         reporter.Infos.Should().Contain("Locale 'nl': +1 ~1 -1");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   private sealed class LoggingTranslationApiService : ITranslationApiService
   {
      public Task<ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken)
      {
         return Task.FromResult(new ProjectMetadataResponse
         {
            DefaultLocale = "en",
            Locales = ["en", "nl"]
         });
      }

      public Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
      {
         TranslationItemResponse[] items = locale switch
         {
            "en" =>
            [
               new TranslationItemResponse
               {
                  Origin = "Demo:/Localizations.resx",
                  Key = "Button.Cancel",
                  Value = "Cancel"
               },
               new TranslationItemResponse
               {
                  Origin = "Demo:/Localizations.resx",
                  Key = "Button.Save",
                  Value = "Save"
               }
            ],
            "nl" =>
            [
               new TranslationItemResponse
               {
                  Origin = "Demo:/Localizations.resx",
                  Key = "Button.Cancel",
                  Value = "Annuleren"
               },
               new TranslationItemResponse
               {
                  Origin = "Demo:/Localizations.resx",
                  Key = "Button.Save",
                  Value = "Opslaan"
               }
            ],
            _ => []
         };

         return Task.FromResult(items);
      }

      public Task<mvdmio.TranslationTools.Tool.Push.TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, mvdmio.TranslationTools.Tool.Push.TranslationPushRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }
   }

   private sealed class TestPullReporter : IPullReporter
   {
      public List<string> Infos { get; } = [];
      public List<string> Errors { get; } = [];

      public void WriteInfo(string message)
      {
         Infos.Add(message);
      }

      public void WriteError(string message)
      {
         Errors.Add(message);
      }
   }

   private sealed class TestPullFileSystem : IPullFileSystem
   {
      public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

      public void CreateDirectory(string path)
      {
      }

      public bool FileExists(string path)
      {
         return Files.ContainsKey(path);
      }

      public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
      {
         return Task.FromResult(Files[path]);
      }

      public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
      {
         Files[path] = contents;
         return Task.CompletedTask;
      }
   }
}
