using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerResxWritingTests
{
   [Fact]
   public async Task HandleAsync_ShouldWriteOnlyMatchingProjectResxFiles()
   {
      var fileSystem = new RecordingPullFileSystem();
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
         var handler = new PullHandler(new StubTranslationApiService(), fileSystem, new SilentPullReporter());

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

         fileSystem.Writes.Keys.Should().Contain(Path.Combine(projectDirectory, "Localizations.nl.resx"));
         fileSystem.Writes.Keys.Should().Contain(Path.Combine(projectDirectory, PullBaseline.FileName));
         fileSystem.Writes.Keys.Should().NotContain(Path.Combine(projectDirectory, "Localizations.resx"));
         fileSystem.Writes.Keys.Should().NotContain(Path.Combine(projectDirectory, "Ignored.resx"));
         fileSystem.Writes.Keys.Where(static path => path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)).Should().ContainSingle();
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public async Task HandleAsync_ShouldMatchProjectCaseInsensitivelyAndKeepLocalResxCasing()
   {
      var fileSystem = new RecordingPullFileSystem();
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);
      var englishPath = Path.Combine(projectDirectory, "Localizations.resx");
      var dutchPath = Path.Combine(projectDirectory, "Localizations.nl.resx");
      const string originalEnglish = """
         <?xml version="1.0" encoding="utf-8"?>
         <root>
           <data name="Button.Save" xml:space="preserve"><value>Save</value></data>
           <data name="Button.Legacy" xml:space="preserve"><value>Keep me</value></data>
         </root>
         """;

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "mvdmio.Localization.csproj"), "<Project />");
         fileSystem.Writes[englishPath] = originalEnglish;
         fileSystem.Writes[dutchPath] = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Button.Save" xml:space="preserve"><value>Oud</value></data>
            </root>
            """;

         var handler = new PullHandler(new LowercaseOriginTranslationApiService(), fileSystem, new SilentPullReporter());

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

         fileSystem.Writes[englishPath].Should().Be(originalEnglish);
         fileSystem.Writes.Keys.Should().Contain(key => string.Equals(key, dutchPath, StringComparison.Ordinal));
         fileSystem.Writes.Keys.Should().NotContain(key => string.Equals(key, Path.Combine(projectDirectory, "localizations.nl.resx"), StringComparison.Ordinal));
         fileSystem.Writes[dutchPath].Should().Contain("Button.Save");
         fileSystem.Writes[dutchPath].Should().Contain("Opslaan");
         fileSystem.Writes[dutchPath].Should().Contain("Button.Cancel");
         fileSystem.Writes[dutchPath].Should().Contain("Annuleren");
         fileSystem.Writes[dutchPath].Should().NotContain("Keep me");
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   private sealed class StubTranslationApiService : ITranslationApiService
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
                  Key = "Button.Save",
                  Value = "Save"
               },
               new TranslationItemResponse
               {
                  Origin = "OtherProject:/Ignored.resx",
                  Key = "Ignored.Key",
                  Value = "Ignored"
               }
            ],
            "nl" =>
            [
               new TranslationItemResponse
               {
                  Origin = "Demo:/Localizations.resx",
                  Key = "Button.Save",
                  Value = "Opslaan"
               },
               new TranslationItemResponse
               {
                  Origin = "OtherProject:/Ignored.resx",
                  Key = "Ignored.Key",
                  Value = "Genegeerd"
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

   private sealed class LowercaseOriginTranslationApiService : ITranslationApiService
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
                  Origin = "mvdmio.localization:/localizations.resx",
                  Key = "Button.Save",
                  Value = "Save overwritten"
               },
               new TranslationItemResponse
               {
                  Origin = "mvdmio.localization:/localizations.resx",
                  Key = "Button.Cancel",
                  Value = "Cancel"
               }
            ],
            "nl" =>
            [
               new TranslationItemResponse
               {
                  Origin = "mvdmio.localization:/localizations.resx",
                  Key = "Button.Save",
                  Value = "Opslaan"
               },
               new TranslationItemResponse
               {
                  Origin = "mvdmio.localization:/localizations.resx",
                  Key = "Button.Cancel",
                  Value = "Annuleren"
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

   private sealed class RecordingPullFileSystem : IPullFileSystem
   {
      public Dictionary<string, string> Writes { get; } = new(StringComparer.OrdinalIgnoreCase);

      public void CreateDirectory(string path)
      {
      }

      public bool FileExists(string path)
      {
         return Writes.ContainsKey(path);
      }

      public IEnumerable<string> EnumerateFiles(string directory)
      {
         return Writes.Keys.Where(path => string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase));
      }

      public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
      {
         return Task.FromResult(Writes[path]);
      }

      public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
      {
         Writes[path] = contents;
         return Task.CompletedTask;
      }
   }

   private sealed class SilentPullReporter : IPullReporter
   {
      public void WriteInfo(string message)
      {
      }

      public void WriteError(string message)
      {
      }
   }
}
