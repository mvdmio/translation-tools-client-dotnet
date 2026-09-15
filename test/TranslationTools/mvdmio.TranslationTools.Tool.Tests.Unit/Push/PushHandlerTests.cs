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
      using var fixture = new PushProjectFixture();

      await fixture.Handler.HandleAsync(fixture.CreateConfiguration(), prune: true, CancellationToken.None);

      fixture.ApiService.Request.Should().NotBeNull();
      fixture.ApiService.Request!.Prune.Should().BeTrue();
      fixture.ApiService.Request.Items.Should().ContainSingle(x => x.Key == "Home.Title" && x.Locale == "en" && x.Value == "Hello");
   }

   [Fact]
   public async Task HandleAsync_ShouldSendConfiguredEnvironmentInPushRequest()
   {
      using var fixture = new PushProjectFixture();

      await fixture.Handler.HandleAsync(fixture.CreateConfiguration("production"), prune: false, CancellationToken.None);

      fixture.ApiService.Request.Should().NotBeNull();
      fixture.ApiService.Request!.Environment.Should().Be("production");
   }

   [Fact]
   public async Task HandleAsync_WhenEnvironmentOmitted_ShouldPushIntoUnnamedEnvironment()
   {
      using var fixture = new PushProjectFixture();

      await fixture.Handler.HandleAsync(fixture.CreateConfiguration(), prune: false, CancellationToken.None);

      fixture.ApiService.Request.Should().NotBeNull();
      fixture.ApiService.Request!.Environment.Should().BeNull();
   }

   [Fact]
   public async Task HandleAsync_WhenEnvironmentIsIllegal_ShouldReportErrorAndNotCallApi()
   {
      using var fixture = new PushProjectFixture();

      await fixture.Handler.HandleAsync(fixture.CreateConfiguration("prod:sha"), prune: false, CancellationToken.None);

      fixture.ApiService.Request.Should().BeNull();
      fixture.Reporter.Errors.Should().ContainSingle()
         .Which.Should().Contain("letters")
         .And.Contain("64")
         .And.Contain("dots")
         .And.Contain("underscores")
         .And.Contain("hyphens");
   }

   private sealed class PushProjectFixture : IDisposable
   {
      public string ProjectDirectory { get; }
      public TestTranslationApiService ApiService { get; }
      public TestPushReporter Reporter { get; }
      public PushHandler Handler { get; }

      public PushProjectFixture()
      {
         ProjectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
         Directory.CreateDirectory(ProjectDirectory);
         File.WriteAllText(Path.Combine(ProjectDirectory, "Demo.csproj"), "<Project />");
         File.WriteAllText(
            Path.Combine(ProjectDirectory, "Localizations.resx"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Home.Title"><value>Hello</value></data>
            </root>
            """
         );

         ApiService = new TestTranslationApiService();
         Reporter = new TestPushReporter();
         Handler = new PushHandler(ApiService, new ProjectManifestScanner(), Reporter);
      }

      public ToolConfiguration CreateConfiguration(string? environment = null)
      {
         return new ToolConfiguration
         {
            ApiKey = "test-api-key",
            ConfigDirectory = ProjectDirectory,
            DefaultLocale = "en",
            Environment = environment
         };
      }

      public void Dispose()
      {
         if (Directory.Exists(ProjectDirectory))
            Directory.Delete(ProjectDirectory, recursive: true);
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
      public List<string> Errors { get; } = [];

      public void WriteInfo(string message)
      {
      }

      public void WriteError(string message)
      {
         Errors.Add(message);
      }
   }
}
