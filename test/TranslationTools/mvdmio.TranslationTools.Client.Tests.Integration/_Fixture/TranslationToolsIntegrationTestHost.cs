using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace mvdmio.TranslationTools.Client.Tests.Integration._Fixture;

internal sealed class TranslationToolsIntegrationTestHost : IAsyncDisposable
{
   private const string SocketToken = "test-token";

   private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

   private readonly TaskCompletionSource<WebSocket> _webSocketConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
   private readonly CancellationTokenSource _disposeCancellationTokenSource = new();

   private WebApplication? _app;

   public string? LastAuthorizationHeader { get; private set; }

   public string? LastEnvironment { get; private set; }

   public int LocaleRequestCount => _localeRequestCount;

   public int TranslationRequestCount => _translationRequestCount;

   public string? LastTranslationRequestQuery { get; private set; }

   public ConcurrentQueue<SingleKeyRequest> SingleKeyRequests { get; } = new();

   public int SocketTokenRequestCount => _socketTokenRequestCount;

   public int HeartbeatRequestCount => _heartbeatRequestCount;

   public string? LastHeartbeatAuthorizationHeader { get; private set; }

   public HeartbeatBody? LastHeartbeatBody { get; private set; }

   public ConcurrentBag<ProjectPushBody> ProjectPushes { get; } = new();

   public ConcurrentBag<string> ProjectPushRawBodies { get; } = new();

   public ConcurrentBag<DeclarationBody> Declarations { get; } = new();

   public ConcurrentBag<string> DeclarationRawBodies { get; } = new();

   public required IReadOnlyDictionary<string, IReadOnlyDictionary<TranslationRef, string?>> Locales { get; init; }

   public string BaseUrl { get; private set; } = string.Empty;

   public int HeartbeatStatusCode { get; set; } = StatusCodes.Status200OK;

   public int LocaleStatusCode { get; set; } = StatusCodes.Status200OK;

   public ConcurrentDictionary<string, int> LocaleStatusCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

   public int ProjectPushStatusCode { get; set; } = StatusCodes.Status200OK;

   public int DeclarationStatusCode { get; set; } = StatusCodes.Status200OK;

   public int TranslationStatusCode { get; set; } = StatusCodes.Status200OK;

   public Func<SingleKeyRequest, string?>? TranslationAnswer { get; set; }

   public Task? TranslationDelay { get; set; }

   private int _localeRequestCount;

   private int _translationRequestCount;

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

         if (host.LocaleStatusCodes.TryGetValue(locale, out var localeStatusCode))
            return Results.StatusCode(localeStatusCode);

         if (host.LocaleStatusCode != StatusCodes.Status200OK)
            return Results.StatusCode(host.LocaleStatusCode);

         if (!host.Locales.TryGetValue(locale, out var values))
            return Results.Json(Array.Empty<TranslationItemResponse>());

         var payload = values.Select(static item => new TranslationItemResponse
         {
            Origin = item.Key.Origin,
            Key = item.Key.Key,
            Value = item.Value
         });

         return Results.Json(payload, _serializerOptions);
      }

      app.MapGet("/api/v1/translations/{locale}", (HttpContext context, string locale) => ServeLocale(context, locale, environment: null));
      app.MapGet("/api/v1/translations/{locale}/{environment}", (HttpContext context, string locale, string environment) => ServeLocale(context, locale, environment));

      async Task<IResult> ServeTranslationAsync(HttpContext context, string origin, string locale, string key, string? environment)
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();
         host.LastTranslationRequestQuery = context.Request.QueryString.Value;
         Interlocked.Increment(ref host._translationRequestCount);

         origin = Uri.UnescapeDataString(origin);

         var singleKeyRequest = new SingleKeyRequest(origin, locale, key, environment, context.Request.QueryString.Value);
         host.SingleKeyRequests.Enqueue(singleKeyRequest);

         if (host.TranslationDelay is { } delay)
         {
            try
            {
               await delay.WaitAsync(context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
               return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
         }

         if (host.TranslationStatusCode != StatusCodes.Status200OK)
            return Results.StatusCode(host.TranslationStatusCode);

         if (host.TranslationAnswer is { } answer)
         {
            return Results.Json(new TranslationItemResponse
            {
               Origin = origin,
               Key = key,
               Value = answer(singleKeyRequest)
            }, _serializerOptions);
         }

         if (host.Locales.TryGetValue(locale, out var values) && values.TryGetValue(new TranslationRef(origin, key), out var value))
         {
            return Results.Json(new TranslationItemResponse
            {
               Origin = origin,
               Key = key,
               Value = value
            }, _serializerOptions);
         }

         return Results.NotFound();
      }

      app.MapGet("/api/v1/translations/{origin}/{locale}/{key}", async (HttpContext context, string origin, string locale, string key) => await ServeTranslationAsync(context, origin, locale, key, environment: null));
      app.MapGet("/api/v1/translations/{origin}/{locale}/{key}/{environment}", async (HttpContext context, string origin, string locale, string key, string environment) => await ServeTranslationAsync(context, origin, locale, key, environment));

      app.MapGet("/api/v1/translations/socket-token", (HttpContext context) =>
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();
         Interlocked.Increment(ref host._socketTokenRequestCount);
         return Results.Json(new { token = SocketToken }, _serializerOptions);
      });

      app.MapPost("/api/v1/translations/heartbeat", async (HttpContext context) =>
      {
         host.LastHeartbeatAuthorizationHeader = context.Request.Headers.Authorization.ToString();

         var body = await context.Request.ReadFromJsonAsync<HeartbeatBody>(_serializerOptions);
         host.LastHeartbeatBody = body;
         Interlocked.Increment(ref host._heartbeatRequestCount);

         return Results.StatusCode(host.HeartbeatStatusCode);
      });

      app.MapPost("/api/v1/translations/project", async (HttpContext context) =>
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();

         using var reader = new StreamReader(context.Request.Body);
         var raw = await reader.ReadToEndAsync();
         host.ProjectPushRawBodies.Add(raw);

         var parsed = JsonSerializer.Deserialize<ProjectPushBody>(raw, _serializerOptions) ?? new ProjectPushBody();
         host.ProjectPushes.Add(parsed);

         return Results.StatusCode(host.ProjectPushStatusCode);
      });

      app.MapPost("/api/v1/translations/declaration", async (HttpContext context) =>
      {
         host.LastAuthorizationHeader = context.Request.Headers.Authorization.ToString();

         using var reader = new StreamReader(context.Request.Body);
         var raw = await reader.ReadToEndAsync();
         host.DeclarationRawBodies.Add(raw);

         var parsed = JsonSerializer.Deserialize<DeclarationBody>(raw, _serializerOptions) ?? new DeclarationBody();
         host.Declarations.Add(parsed);

         return Results.StatusCode(host.DeclarationStatusCode);
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
         }
         catch (WebSocketException)
         {
         }
      }

      if (_app is not null)
      {
         await _app.StopAsync();
         await _app.DisposeAsync();
      }

      _disposeCancellationTokenSource.Dispose();
   }

   public sealed record SingleKeyRequest(string Origin, string Locale, string Key, string? Environment, string? Query);

   public sealed record HeartbeatBody(string? ClientId, string? Environment, string? Platform, string? Version);

   public sealed class DeclarationBody
   {
      public KeyValueItemBody[] Items { get; init; } = [];

      public string? Environment { get; init; }
   }

   public sealed class ProjectPushBody
   {
      public KeyValueItemBody[] Items { get; init; } = [];

      public string? Environment { get; init; }

      public bool Prune { get; init; }

      public string[]? Globals { get; init; }
   }

   public sealed class KeyValueItemBody
   {
      public string Origin { get; init; } = string.Empty;

      public string Locale { get; init; } = string.Empty;

      public string Key { get; init; } = string.Empty;

      public string? Value { get; init; }
   }
}
