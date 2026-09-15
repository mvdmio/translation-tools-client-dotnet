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

   /// <summary>
   /// Fetches a single translation. Never throws (other than for the caller's own cancellation, and
   /// unless <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> is set): an unsuccessful
   /// response, a transport exception, a timeout, and an undeserialisable body are all logged and
   /// answered with the local fallback instead. Degraded results are never cached, so the next call
   /// retries the service.
   ///
   /// Bounded by <see cref="TranslationToolsClientOptions.LookupTimeout"/>, applied as a
   /// <see cref="TimeProvider"/>-derived cancellation token linked to the caller's own token —
   /// never by setting <see cref="HttpClient.Timeout"/>, which belongs to the consumer that
   /// supplied the <see cref="HttpClient"/>. The bound covers reading the response body as well as
   /// getting its headers. A caller's own cancellation is distinguished from the client's timeout
   /// and always propagates rather than degrading.
   /// </summary>
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
         // The caller asked to stop. That is not a failure, and it is never swallowed.
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

   /// <summary>
   /// Logs a classified failure at its level and answers the lookup with the local fallback.
   ///
   /// With <see cref="TranslationToolsClientOptions.ThrowOnLookupError"/> set it rethrows instead,
   /// preserving the exception the fetch produced so an application that opts back into throwing
   /// catches the same types it caught before this contract existed: an
   /// <see cref="HttpRequestException"/> for an unsuccessful response or a connection failure, a
   /// <see cref="JsonException"/> for a body the client cannot read. The client's own timeout and
   /// the suppression window have no such exception to preserve, and surface as a
   /// <see cref="TranslationLookupException"/>, which also keeps a timeout distinguishable from the
   /// caller's own cancellation.
   /// </summary>
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
