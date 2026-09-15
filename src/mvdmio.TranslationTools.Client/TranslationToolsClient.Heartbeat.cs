using Microsoft.Extensions.Logging;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

public sealed partial class TranslationToolsClient
{
   private void StartHeartbeat()
   {
      if (!Options.EnableHeartbeat)
         return;

      if (Interlocked.CompareExchange(ref _heartbeatStarted, 1, 0) != 0)
         return;

      _heartbeatCts = new CancellationTokenSource();
      var token = _heartbeatCts.Token;
      _ = Task.Run(() => HeartbeatLoopAsync(token), CancellationToken.None);
   }

   private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
   {
      try
      {
         await SafeSendHeartbeatAsync(cancellationToken);

         using var timer = new PeriodicTimer(Options.HeartbeatInterval, _timeProvider);
         while (await timer.WaitForNextTickAsync(cancellationToken))
            await SafeSendHeartbeatAsync(cancellationToken);
      }
      catch (OperationCanceledException)
      {
         // Heartbeat loop cancelled during shutdown; nothing to do.
      }
   }

   private async Task SafeSendHeartbeatAsync(CancellationToken cancellationToken)
   {
      try
      {
         await SendHeartbeatAsync(cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         // A failed heartbeat must never bubble into app code; best-effort log and retry next tick.
         _logger?.LogWarning(exception, "TranslationTools heartbeat failed.");
      }
   }

   internal async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
   {
      var payload = new HeartbeatRequest
      {
         ClientId = _clientId,
         Environment = _environment,
         Platform = PlatformName,
         Version = _clientVersion
      };

      var json = JsonSerializer.Serialize(payload, _serializerOptions);

      using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/translations/heartbeat")
      {
         Content = new StringContent(json, Encoding.UTF8, "application/json")
      };

      using var response = await _client.SendAsync(request, cancellationToken);
      response.EnsureSuccessStatusCode();
   }
}
