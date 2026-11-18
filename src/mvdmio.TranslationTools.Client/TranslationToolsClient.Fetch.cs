using Microsoft.Extensions.Logging;
using mvdmio.TranslationTools.Client.Internal;
using System;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

public sealed partial class TranslationToolsClient
{
   private async Task<TranslationItemResponse[]> FetchLocaleAsync(EffectiveLocale locale, CancellationToken cancellationToken)
   {
      if (_suppression.IsOpen)
         throw new TranslationLookupException($"Translation lookup for locale '{locale.Name}' failed: {TranslationLookupFailure.Suppressed.Reason}.");

      var url = $"api/v1/translations/{Uri.EscapeDataString(locale.Name)}";

      if (_environment is not null)
         url += $"/{Uri.EscapeDataString(_environment)}";

      using var request = new HttpRequestMessage(HttpMethod.Get, url);
      return await FetchAsync<TranslationItemResponse[]>(request, cancellationToken);
   }

   private async Task<(TranslationItemResponse Value, bool Degraded)> FetchTranslationOrFallbackAsync(TranslationLookupRequest lookup, CancellationToken cancellationToken)
   {
      if (_suppression.IsOpen)
         return HandleLookupFailure(lookup, TranslationLookupFailure.Suppressed, exception: null);

      using var request = lookup.ToHttpRequest(_environment);

      using var timeoutCts = new CancellationTokenSource(Options.LookupTimeout, _timeProvider);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

      try
      {
         return (await FetchAsync<TranslationItemResponse>(request, linkedCts.Token), false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception exception)
      {
         var failure = TranslationLookupFailure.Classify(exception);

         if (failure.OpensSuppressionWindow)
            _suppression.Open();

         return HandleLookupFailure(lookup, failure, exception);
      }
   }

   private (TranslationItemResponse Value, bool Degraded) HandleLookupFailure(TranslationLookupRequest lookup, TranslationLookupFailure failure, Exception? exception)
   {
      if (Options.ThrowOnLookupError)
      {
         if (exception is not null and not OperationCanceledException)
            ExceptionDispatchInfo.Capture(exception).Throw();

         throw new TranslationLookupException(
            $"Translation lookup for key '{lookup.Translation.Key}' in locale '{lookup.Locale.Name}' failed: {failure.Reason}.",
            exception
         );
      }

      _logger?.Log(
         failure.Level,
         exception,
         "Translation lookup for key '{Key}' in locale '{Locale}' degraded to local fallback: {Reason}.",
         lookup.Translation.Key,
         lookup.Locale.Name,
         failure.Reason
      );

      return (lookup.LocalFallback(), true);
   }

   private async Task<T> FetchAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken) where T : class
   {
      using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      return await DeserializeAsync<T>(response.Content, cancellationToken) ?? throw new InvalidOperationException("Response body was empty.");
   }

   private static async Task<T?> DeserializeAsync<T>(HttpContent content, CancellationToken cancellationToken) where T : class
   {
      await using var stream = await content.ReadAsStreamAsync(cancellationToken);
      return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
   }
}
