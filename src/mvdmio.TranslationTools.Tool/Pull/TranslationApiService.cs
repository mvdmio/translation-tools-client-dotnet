using System.Net.Http.Json;
using mvdmio.TranslationTools.Client;
using mvdmio.TranslationTools.Tool.Push;

namespace mvdmio.TranslationTools.Tool.Pull;

internal interface ITranslationApiService
{
   Task<ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken);
   Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken);
   Task<TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, TranslationPushRequest request, CancellationToken cancellationToken);
   Task<mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportResponse> ImportProjectStateAsync(string apiKey, mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportRequest request, CancellationToken cancellationToken);
}

internal sealed class TranslationApiService : ITranslationApiService
{
   public async Task<ProjectMetadataResponse> FetchProjectMetadataAsync(string apiKey, CancellationToken cancellationToken)
   {
      using var httpClient = CreateClient(apiKey);
      var result = await httpClient.GetFromJsonAsync<ProjectMetadataResponse>("api/v1/translations/project", cancellationToken);
      return result ?? throw new InvalidOperationException("Project metadata response body was empty.");
   }

   public async Task<TranslationItemResponse[]> FetchLocaleAsync(string apiKey, string locale, CancellationToken cancellationToken)
   {
      using var httpClient = CreateClient(apiKey);
      var result = await httpClient.GetFromJsonAsync<TranslationItemResponse[]>($"api/v1/translations/{Uri.EscapeDataString(locale)}", cancellationToken);
      return result ?? [];
   }

   public async Task<TranslationPushResponse> PushProjectTranslationsAsync(string apiKey, TranslationPushRequest request, CancellationToken cancellationToken)
   {
      using var httpClient = CreateClient(apiKey);
      using var response = await httpClient.PostAsJsonAsync("api/v1/translations/project", request, cancellationToken);
      response.EnsureSuccessStatusCode();

      var result = await response.Content.ReadFromJsonAsync<TranslationPushResponse>(cancellationToken);
      return result ?? throw new InvalidOperationException("Translation push response body was empty.");
   }

   public async Task<mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportResponse> ImportProjectStateAsync(string apiKey, mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportRequest request, CancellationToken cancellationToken)
   {
      using var httpClient = CreateClient(apiKey);
      using var response = await httpClient.PostAsJsonAsync("api/v1/translations/project/import", request, cancellationToken);
      response.EnsureSuccessStatusCode();

      var result = await response.Content.ReadFromJsonAsync<mvdmio.TranslationTools.Tool.Migrate.ProjectTranslationStateImportResponse>(cancellationToken);
      return result ?? throw new InvalidOperationException("Translation import response body was empty.");
   }

   private static HttpClient CreateClient(string apiKey)
   {
      var client = new HttpClient {
         BaseAddress = new Uri(Configuration.ToolConfiguration.DEFAULT_BASE_URL)
      };
      client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
      return client;
   }
}
