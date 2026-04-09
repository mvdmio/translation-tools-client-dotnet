using AwesomeAssertions;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerResxWritingTests
{
   [Fact]
   public async Task HandleAsync_ShouldWriteOnlyResxFiles()
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

         fileSystem.Writes.Should().HaveCount(2);
         fileSystem.Writes.Keys.Should().OnlyContain(static path => path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase));
         fileSystem.Writes.Keys.Should().Contain(Path.Combine(projectDirectory, "Localizations.resx"));
         fileSystem.Writes.Keys.Should().Contain(Path.Combine(projectDirectory, "Localizations.nl.resx"));
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
                  Origin = "/Localizations.resx",
                  Key = "Button.Save",
                  Value = "Save"
               }
            ],
            "nl" =>
            [
               new TranslationItemResponse
               {
                  Origin = "/Localizations.resx",
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

   private sealed class RecordingPullFileSystem : IPullFileSystem
   {
      public Dictionary<string, string> Writes { get; } = new(StringComparer.OrdinalIgnoreCase);

      public void CreateDirectory(string path)
      {
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
