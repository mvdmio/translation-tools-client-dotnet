using AwesomeAssertions;
using Microsoft.Extensions.Options;
using mvdmio.TranslationTools.Client;
using System.Net;
using System.Text.Json;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class ClassLibraryDeclarationEndToEndTests
{
   private const string LIBRARY_ASSEMBLY_NAME = "SourceGeneratorClassLibrary";
   private const string LIBRARY_ORIGIN = LIBRARY_ASSEMBLY_NAME + ":/Strings.resx";

   [Fact]
   public async Task Initialize_OnPublicPath_DeclaresKeysOfClassLibraryWhoseCodeNeverRan()
   {
      // Nothing in this test project touches a type of the library, and no other test starts a client on
      // the public path, so the library is neither loaded nor registered until Initialize runs.
      AppDomain.CurrentDomain.GetAssemblies().Should().NotContain(assembly => assembly.GetName().Name == LIBRARY_ASSEMBLY_NAME);
      TranslationCatalog.Entries.Should().NotContain(entry => entry.Origin == LIBRARY_ORIGIN);

      var handler = new RecordingHandler();
      using var client = new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(
            new TranslationToolsClientOptions
            {
               ApiKey = "api-key",
               DefaultLocale = "en",
               EnableHeartbeat = false
            }
         )
      );

      await client.Initialize(TestContext.Current.CancellationToken);

      handler.DeclarationBodies.Should().ContainSingle();

      using var declaration = JsonDocument.Parse(handler.DeclarationBodies.Single());
      var items = declaration.RootElement.GetProperty("items").EnumerateArray().ToArray();

      items.Should().Contain(item =>
         item.GetProperty("origin").GetString() == LIBRARY_ORIGIN
         && item.GetProperty("key").GetString() == "Library.Greeting"
         && item.GetProperty("locale").GetString() == "en"
         && item.GetProperty("value").GetString() == "Hello from the library");

      items.Should().Contain(item =>
         item.GetProperty("origin").GetString() == LIBRARY_ORIGIN
         && item.GetProperty("key").GetString() == "Library.Greeting"
         && item.GetProperty("locale").GetString() == "nl"
         && item.GetProperty("value").GetString() == "Hallo vanuit de bibliotheek");
   }

   private sealed class RecordingHandler : HttpMessageHandler
   {
      public List<string> DeclarationBodies { get; } = [];

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         var path = request.RequestUri?.AbsolutePath ?? string.Empty;

         if (request.Method == HttpMethod.Post && path.EndsWith("/api/v1/translations/declaration", StringComparison.Ordinal))
         {
            DeclarationBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
         }

         return new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StringContent("[]")
         };
      }
   }
}
