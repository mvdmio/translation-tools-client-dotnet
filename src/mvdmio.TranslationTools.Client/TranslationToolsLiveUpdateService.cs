using System;
using System.Buffers;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace mvdmio.TranslationTools.Client;

internal sealed class TranslationToolsLiveUpdateService : IDisposable
{
   private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
      PropertyNameCaseInsensitive = true
   };

   private readonly IHttpClientFactory _httpClientFactory;
   private readonly ITranslationToolsClient _client;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly SemaphoreSlim _startLock = new(1, 1);

   private CancellationTokenSource? _cancellationTokenSource;
   private Task? _backgroundTask;

   public TranslationToolsLiveUpdateService(IHttpClientFactory httpClientFactory, ITranslationToolsClient client, IOptions<TranslationToolsClientOptions> options)
   {
      _httpClientFactory = httpClientFactory;
      _client = client;
      _options = options;
   }

   public async Task StartAsync(CancellationToken cancellationToken)
   {
      if (!_options.Value.EnableLiveUpdates)
         return;

      await _startLock.WaitAsync(cancellationToken);

      try
      {
         if (_backgroundTask is not null)
            return;

         _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
         _backgroundTask = Task.Run(() => RunAsync(_cancellationTokenSource.Token), CancellationToken.None);
      }
      finally
      {
         _startLock.Release();
      }
   }

   public void Dispose()
   {
      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource?.Dispose();
      _startLock.Dispose();
   }

   private async Task RunAsync(CancellationToken cancellationToken)
   {
      while (!cancellationToken.IsCancellationRequested)
      {
         try
         {
            var socketToken = await GetSocketTokenAsync(cancellationToken);
            using var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(BuildSocketUri(socketToken.Token), cancellationToken);
            await ReceiveLoopAsync(webSocket, cancellationToken);
         }
         catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
         {
            break;
         }
         catch (HttpRequestException)
         {
         }
         catch (WebSocketException)
         {
         }
         catch (JsonException)
         {
         }

         try
         {
            await Task.Delay(ReconnectDelay, cancellationToken);
         }
         catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
         {
            break;
         }
      }
   }

   private async Task ReceiveLoopAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
   {
      var buffer = new byte[4096];
      var messageBuffer = new ArrayBufferWriter<byte>();

      while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
      {
         messageBuffer.Clear();
         WebSocketReceiveResult result;

         do
         {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
               return;

            messageBuffer.Write(buffer.AsSpan(0, result.Count));
         } while (!result.EndOfMessage);

         if (result.MessageType != WebSocketMessageType.Text)
            continue;

         var payload = Encoding.UTF8.GetString(messageBuffer.WrittenSpan);
         await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(_client, payload, cancellationToken);
      }
   }

   private async Task<TranslationToolsSocketTokenResponse> GetSocketTokenAsync(CancellationToken cancellationToken)
   {
      using var httpClient = _httpClientFactory.CreateClient();
      httpClient.BaseAddress = new Uri(TranslationToolsClientOptions.DEFAULT_BASE_URL);

      using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/translations/socket-token");
      request.Headers.TryAddWithoutValidation("Authorization", _options.Value.ApiKey);

      using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      return await JsonSerializer.DeserializeAsync<TranslationToolsSocketTokenResponse>(stream, SerializerOptions, cancellationToken)
             ?? throw new InvalidOperationException("Socket token response body was empty.");
   }

   private static Uri BuildSocketUri(string socketToken)
   {
      var builder = new UriBuilder(TranslationToolsClientOptions.DEFAULT_BASE_URL);
      builder.Scheme = string.Equals(builder.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
      builder.Path = "/ws/translations";
      builder.Query = $"token={Uri.EscapeDataString(socketToken)}";

      return builder.Uri;
   }

   private sealed class TranslationToolsSocketTokenResponse
   {
      public required string Token { get; init; }
   }
}
