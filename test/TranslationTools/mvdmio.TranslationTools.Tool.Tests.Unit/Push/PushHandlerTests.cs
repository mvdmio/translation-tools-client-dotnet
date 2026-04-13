using AwesomeAssertions;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Push;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Push;

public class PushHandlerTests
{
   [Fact]
   public void ResolveProjectDirectory_ShouldResolveNearestCsprojFromConfigDirectory()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

      try
      {
         Directory.CreateDirectory(projectDirectory);
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
         File.WriteAllText(Path.Combine(projectDirectory, ToolConfiguration.CONFIG_FILE_NAME), "apiKey: test\ndefaultLocale: en\n");

         var result = PushHandler.ResolveProjectDirectory(
            new ToolConfiguration
            {
               ConfigDirectory = projectDirectory,
               DefaultLocale = "en"
            }
         );

         result.Should().Be(projectDirectory);
      }
      finally
      {
         if (Directory.Exists(projectDirectory))
            Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public async Task HandleAsync_ShouldSendPruneFlagInPushRequest()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

      try
      {
         Directory.CreateDirectory(projectDirectory);
         File.WriteAllText(Path.Combine(projectDirectory, "Demo.csproj"), "<Project />");
         File.WriteAllText(
            Path.Combine(projectDirectory, "Localizations.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Home.Title"><value>Hello</value></data>
            </root>
            """
         );

         var apiService = new TestTranslationApiService();
         var reporter = new TestPushReporter();
         var handler = new PushHandler(apiService, new ProjectManifestScanner(), reporter);
         var config = new ToolConfiguration
         {
            ApiKey = "test-api-key",
            ConfigDirectory = projectDirectory,
            DefaultLocale = "en"
         };

         await handler.HandleAsync(config, prune: true, CancellationToken.None);

         apiService.Request.Should().NotBeNull();
         apiService.Request!.Prune.Should().BeTrue();
         apiService.Request.Items.Should().ContainSingle(x => x.Key == "Home.Title" && x.Locale == "en" && x.Value == "Hello");
      }
      finally
      {
         if (Directory.Exists(projectDirectory))
            Directory.Delete(projectDirectory, recursive: true);
      }
   }

   private sealed class TestTranslationApiService : mvdmio.TranslationTools.Tool.Pull.ITranslationApiService
   {
      public TranslationPushRequest? Request { get; private set; }

      public Task<mvdmio.TranslationTools.Tool.Pull.ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }

      public Task<mvdmio.TranslationTools.Client.TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }

      public Task<TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, TranslationPushRequest request, CancellationToken cancellationToken)
      {
         Request = request;
         return Task.FromResult(
            new TranslationPushResponse
            {
               ReceivedKeyCount = request.Items.Length,
               CreatedKeyCount = request.Items.Length,
               UpdatedKeyCount = 0,
               RemovedKeyCount = 0
            }
         );
      }
   }

   private sealed class TestPushReporter : IPushReporter
   {
      public void WriteInfo(string message)
      {
      }

      public void WriteError(string message)
      {
      }
   }
}
