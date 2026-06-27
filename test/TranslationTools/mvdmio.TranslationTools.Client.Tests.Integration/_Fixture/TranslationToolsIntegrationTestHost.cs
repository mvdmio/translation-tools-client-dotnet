using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace mvdmio.TranslationTools.Client.Tests.Integration._Fixture;

internal sealed class TranslationToolsIntegrationTestHost : IAsyncDisposable
{
   private const string SocketToken = "test-token";

   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

   private readonly TaskCompletionSource<WebSocket> _webSocketConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
   private readonly CancellationTokenSource _disposeCancellationTokenSource = new();

   private WebApplication? _app;

   public string? LastAuthorizationHeader { get; private set; }

   public string? LastEnvironment { get; private set; }

   public int LocaleRequestCount => _localeRequestCount;

   public int SocketTokenRequestCount => _socketTokenRequestCount;

   public int HeartbeatRequestCount => _heartbeatRequestCount;

   public string? LastHeartbeatAuthorizationHeader { get; private set; }

   public HeartbeatBody? LastHeartbeatBody { get; private set; }

   public required IReadOnlyDictionary<string, IReadOnlyDictionary<TranslationRef, string?>> Locales { get; init; }

   public string BaseUrl { get; private set; } = string.Empty;

   public int HeartbeatStatusCode { get; set; } = StatusCodes.Status200OK;

   private int _localeRequestCount;

   private int _socketTokenRequestCount;

   private int _heartbeatRequestCount;

   public static async Task<TranslationToolsIntegrationTestHost> StartAsync(IReadOnlyDictionary<string, IReadOnlyDictionary<TranslationRef, string?>> locales, CancellationToken cancellationToken)
   {
      var builder = WebApplication.CreateSlimBuilder();
      builder.WebHost.UseUrls("http://127.0.0.1:0");

      var host = new TranslationToolsIntegrationTestHost
      {
         Locales = locales
      };

      var app = builder.Build();
      app.UseWebSockets();

      IResult ServeLocale(HttpContext context, string locale, string? environment)
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();
         host.LastEnvironment = environment;
         Interlocked.Increment(ref host._localeRequestCount);

         if (!host.Locales.TryGetValue(locale, out var values))
            return Results.Json(Array.Empty<TranslationItemResponse>());

         var payload = values.Select(static item => new TranslationItemResponse
         {
            Origin = item.Key.Origin,
            Key = item.Key.Key,
            Value = item.Value
         });

         return Results.Json(payload, SerializerOptions);
      }

      app.MapGet("/api/v1/translations/{locale}", (HttpContext context, string locale) => ServeLocale(context, locale, environment: null));
      app.MapGet("/api/v1/translations/{locale}/{environment}", (HttpContext context, string locale, string environment) => ServeLocale(context, locale, environment));

      app.MapGet("/api/v1/translations/socket-token", (HttpContext context) =>
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();
         Interlocked.Increment(ref host._socketTokenRequestCount);
         return Results.Json(new { token = SocketToken }, SerializerOptions);
      });

      app.MapPost("/api/v1/translations/heartbeat", async (HttpContext context) =>
      {
         host.LastHeartbeatAuthorizationHeader = context.Request.Headers.Authorization.ToString();

         var body = await context.Request.ReadFromJsonAsync<HeartbeatBody>(SerializerOptions);
         host.LastHeartbeatBody = body;
         Interlocked.Increment(ref host._heartbeatRequestCount);

         return Results.StatusCode(host.HeartbeatStatusCode);
      });

      app.MapGet("/ws/translations", async context =>
      {
         if (!context.WebSockets.IsWebSocketRequest)
         {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
         }

         if (!string.Equals(context.Request.Query["token"], SocketToken, StringComparison.Ordinal))
         {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
         }

         var webSocket = await context.WebSockets.AcceptWebSocketAsync();
         host._webSocketConnected.TrySetResult(webSocket);

         var buffer = new byte[128];
         try
         {
            while (webSocket.State == WebSocketState.Open && !host._disposeCancellationTokenSource.IsCancellationRequested)
            {
               var result = await webSocket.ReceiveAsync(buffer, host._disposeCancellationTokenSource.Token);
               if (result.MessageType == WebSocketMessageType.Close)
               {
                  await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                  break;
               }
            }
         }
         catch (OperationCanceledException)
         {
         }
      });

      await app.StartAsync(cancellationToken);
      host._app = app;

      var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
      host.BaseUrl = addresses!.Addresses.Single();
      return host;
   }

   public async Task SendLiveUpdateAsync(string payload, CancellationToken cancellationToken)
   {
      var webSocket = await _webSocketConnected.Task.WaitAsync(cancellationToken);
      var buffer = Encoding.UTF8.GetBytes(payload);
      await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
   }

   public async ValueTask DisposeAsync()
   {
      _disposeCancellationTokenSource.Cancel();

      if (_webSocketConnected.Task.IsCompletedSuccessfully)
      {
         var webSocket = await _webSocketConnected.Task;
         try
         {
            if (webSocket.State == WebSocketState.Open)
               await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);

            webSocket.Dispose();
         }
         catch (ObjectDisposedException)
         {
            // The underlying HttpContext may already be torn down by the time we dispose the
            // server-side socket; closing it then aborts a disposed context. Safe to ignore.
         }
         catch (WebSocketException)
         {
            // Socket already aborted during shutdown; nothing further to clean up.
         }
      }

      if (_app is not null)
      {
         await _app.StopAsync();
         await _app.DisposeAsync();
      }

      _disposeCancellationTokenSource.Dispose();
   }

   public sealed record HeartbeatBody(string? ClientId, string? Environment, string? Platform, string? Version);
}
