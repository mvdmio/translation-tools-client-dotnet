using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class TranslationToolsLiveUpdateService : IDisposable
{
   private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
   {
      PropertyNameCaseInsensitive = true
   };

   private readonly IHttpClientFactory _httpClientFactory;
   private readonly TranslationToolsClient _client;
   private readonly ILogger<TranslationToolsLiveUpdateService> _logger;
   private readonly IOptions<TranslationToolsClientOptions> _options;
   private readonly SemaphoreSlim _startLock = new(1, 1);

   private CancellationTokenSource? _cancellationTokenSource;
   private Task? _backgroundTask;

   public TranslationToolsLiveUpdateService(IHttpClientFactory httpClientFactory, TranslationToolsClient client, IOptions<TranslationToolsClientOptions> options, ILogger<TranslationToolsLiveUpdateService> logger)
   {
      _httpClientFactory = httpClientFactory;
      _client = client;
      _logger = logger;
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
         {
            _logger.LogDebug("TranslationTools live updates already started.");
            return;
         }

         _logger.LogInformation("Starting TranslationTools live updates.");
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
      if (_backgroundTask is not null)
         _logger.LogDebug("Stopping TranslationTools live updates.");

      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource?.Dispose();
      _startLock.Dispose();
   }

   private async Task RunAsync(CancellationToken cancellationToken)
   {
      while (!cancellationToken.IsCancellationRequested)
      {
         Uri? baseUri = null;

         try
         {
            using var httpClient = _httpClientFactory.CreateClient(nameof(TranslationToolsClient));
            baseUri = new Uri(TranslationToolsClientOptions.DEFAULT_BASE_URL);
            httpClient.BaseAddress = baseUri;

            _logger.LogDebug("Requesting TranslationTools live update socket token from {BaseUrl}.", baseUri);
            var socketToken = await GetSocketTokenAsync(httpClient, cancellationToken);
            using var webSocket = new ClientWebSocket();
            _logger.LogDebug("Connecting TranslationTools live update websocket to {BaseUrl}.", baseUri);
            await webSocket.ConnectAsync(BuildSocketUri(socketToken.Token), cancellationToken);
            _logger.LogInformation("Connected TranslationTools live update websocket to {BaseUrl}.", baseUri);
            await ReceiveLoopAsync(webSocket, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
               _logger.LogInformation(
                  "TranslationTools live update websocket disconnected from {BaseUrl}. Retrying in {ReconnectDelay}.",
                  baseUri,
                  ReconnectDelay
               );
            }
         }
         catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
         {
            break;
         }
         catch (HttpRequestException exception)
         {
            _logger.LogWarning(
               exception,
               "Failed to fetch TranslationTools live update socket token from {BaseUrl}. Retrying in {ReconnectDelay}.",
               baseUri,
               ReconnectDelay
            );
         }
         catch (WebSocketException exception)
         {
            _logger.LogWarning(
               exception,
               "TranslationTools live update websocket failed for {BaseUrl}. Retrying in {ReconnectDelay}.",
               baseUri,
               ReconnectDelay
            );
         }
         catch (JsonException exception)
         {
            _logger.LogWarning(
               exception,
               "Failed to deserialize the TranslationTools live update socket token response from {BaseUrl}. Retrying in {ReconnectDelay}.",
               baseUri,
               ReconnectDelay
            );
         }
         catch (Exception exception)
         {
            _logger.LogError(
               exception,
               "Unexpected TranslationTools live update failure for {BaseUrl}. Retrying in {ReconnectDelay}.",
               baseUri,
               ReconnectDelay
            );
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

      _logger.LogInformation("TranslationTools live updates stopped.");
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
            {
               _logger.LogInformation(
                  "TranslationTools live update websocket received a close frame. Status: {CloseStatus}; Description: {CloseStatusDescription}.",
                  webSocket.CloseStatus,
                  webSocket.CloseStatusDescription
               );
               return;
            }

            messageBuffer.Write(buffer.AsSpan(0, result.Count));
         } while (!result.EndOfMessage);

         if (result.MessageType != WebSocketMessageType.Text)
         {
            _logger.LogDebug("Ignoring non-text TranslationTools live update websocket message of type {MessageType}.", result.MessageType);
            continue;
         }

         var payload = Encoding.UTF8.GetString(messageBuffer.WrittenSpan);
         await TranslationToolsLiveUpdateMessageProcessor.ProcessAsync(_client, payload, _logger, cancellationToken);
      }
   }

   private async Task<TranslationToolsSocketTokenResponse> GetSocketTokenAsync(HttpClient httpClient, CancellationToken cancellationToken)
   {
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
      var builder = new UriBuilder(TranslationToolsClientOptions.DEFAULT_BASE_URL)
      {
         Scheme = "wss",
         Path = "/ws/translations",
         Query = $"token={Uri.EscapeDataString(socketToken)}"
      };

      return builder.Uri;
   }

   private sealed class TranslationToolsSocketTokenResponse
   {
      public required string Token { get; init; }
   }
}
