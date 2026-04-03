using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Configuration;
using mvdmio.TranslationTools.Tool.Pull;
using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit.Pull;

public class PullHandlerLoggingTests
{
   [Fact]
   public async Task HandleAsync_ShouldLogKeyAndValueWhenWritingFails()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Demo.csproj"),
            "<Project><PropertyGroup><RootNamespace>Demo</RootNamespace></PropertyGroup></Project>"
         );
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

         var reporter = new TestPullReporter();
         var handler = new PullHandler(
            new StubTranslationApiService(),
            new mvdmio.TranslationTools.Tool.Migrate.ResxMigrationScanner(),
            new ResxFileParser(),
            new ResxFileWriter(),
            new TestPullFileSystem(),
            reporter
         );

         await Assert.ThrowsAnyAsync<Exception>(
            () => handler.HandleAsync(
               new ToolConfiguration
               {
                  ConfigDirectory = projectDirectory,
                  ApiKey = "api-key",
                  DefaultLocale = "en"
               },
               prune: false,
               TestContext.Current.CancellationToken
            )
         );

         Assert.Contains(
            reporter.Errors,
            message => message.Contains("Key: 'Button_EditStreetSegments'", StringComparison.Ordinal)
               && message.Contains("Value: 'broken", StringComparison.Ordinal)
               && message.Contains("Failed to write Translations.resx", StringComparison.Ordinal)
         );
      }
      finally
      {
         Directory.Delete(projectDirectory, recursive: true);
      }
   }

   [Fact]
   public async Task HandleAsync_ShouldNotLogWriteErrorWhenCanceled()
   {
      var projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(projectDirectory);

      try
      {
         File.WriteAllText(
            Path.Combine(projectDirectory, "Demo.csproj"),
            "<Project><PropertyGroup><RootNamespace>Demo</RootNamespace></PropertyGroup></Project>"
         );
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

         using var cancellationSource = new CancellationTokenSource();
         var reporter = new TestPullReporter();
         var handler = new PullHandler(
            new SuccessfulTranslationApiService(),
            new mvdmio.TranslationTools.Tool.Migrate.ResxMigrationScanner(),
            new ResxFileParser(),
            new ResxFileWriter(),
            new CancelingPullFileSystem(cancellationSource),
            reporter
         );

         await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.HandleAsync(
               new ToolConfiguration
               {
                  ConfigDirectory = projectDirectory,
                  ApiKey = "api-key",
                  DefaultLocale = "en"
               },
               prune: false,
               cancellationSource.Token
            )
         );

         Assert.DoesNotContain(
            reporter.Errors,
            message => message.Contains("Failed to write", StringComparison.Ordinal)
         );
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
         return Task.FromResult(
            new ProjectMetadataResponse
            {
               Locales = ["en"],
               DefaultLocale = "en"
            }
         );
      }

      public Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
      {
         return Task.FromResult(
            new[] {
               new TranslationItemResponse
               {
                  Key = "Button_EditStreetSegments",
                  Value = "broken\u0000value"
               }
            }
         );
      }

      public Task<mvdmio.TranslationTools.Tool.Push.TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, mvdmio.TranslationTools.Tool.Push.TranslationPushRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }

      public Task<mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportResponse> ImportProjectStateAsync(string apiKey, mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }
   }

   private sealed class SuccessfulTranslationApiService : ITranslationApiService
   {
      public Task<ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken)
      {
         return Task.FromResult(
            new ProjectMetadataResponse
            {
               Locales = ["en"],
               DefaultLocale = "en"
            }
         );
      }

      public Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
      {
         return Task.FromResult(
            new[] {
               new TranslationItemResponse
               {
                  Key = "Button_EditStreetSegments",
                  Value = "Edit street segments"
               }
            }
         );
      }

      public Task<mvdmio.TranslationTools.Tool.Push.TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, mvdmio.TranslationTools.Tool.Push.TranslationPushRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
      }

      public Task<mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportResponse> ImportProjectStateAsync(string apiKey, mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportRequest request, CancellationToken cancellationToken)
      {
         throw new NotSupportedException();
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

      public void DeleteFile(string path)
      {
      }
   }

   private sealed class CancelingPullFileSystem(CancellationTokenSource cancellationSource) : IPullFileSystem
   {
      public void CreateDirectory(string path)
      {
      }

      public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
      {
         cancellationSource.Cancel();
         cancellationToken.ThrowIfCancellationRequested();
         return Task.CompletedTask;
      }

      public void DeleteFile(string path)
      {
      }
   }
}
