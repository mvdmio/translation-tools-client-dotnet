using AwesomeAssertions;
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
      var handler = new PullHandler(new TranslationApiService(), new TranslationSnapshotFileWriter(), new TestPullFileSystem(), reporter);

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
      var handler = new PullHandler(new TranslationApiService(), new TranslationSnapshotFileWriter(), new TestPullFileSystem(), reporter);

      try
      {
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project><PropertyGroup><RootNamespace>Demo</RootNamespace></PropertyGroup></Project>");

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

   private sealed class TestPullReporter : IPullReporter
   {
      public List<string> Errors { get; } = [];

      public void WriteInfo(string message)
      {
      }

      public void WriteError(string message)
      {
         Errors.Add(message);
      }
   }

   private sealed class TestPullFileSystem : IPullFileSystem
   {
      public void CreateDirectory(string path)
      {
      }

      public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
      {
         return Task.CompletedTask;
      }
   }
}
