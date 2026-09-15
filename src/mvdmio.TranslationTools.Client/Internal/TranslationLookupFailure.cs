using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Why a single-key lookup could not be answered by the service, and what the client does about it.
/// A failure is classified only to decide how loudly to log it and whether the suppression window
/// opens — never to decide whether to throw.
/// </summary>
internal readonly struct TranslationLookupFailure
{
   /// <summary>
   /// The service answered, but has nothing for this key. Said this way so a reader does not go
   /// looking for a key that exists.
   /// </summary>
   private const string NoValueForThisKey = "the service has no value for this key";

   /// <summary>
   /// The service did not answer, or answered with something the client cannot use.
   /// </summary>
   private const string CouldNotAnswer = "the service could not answer";

   /// <summary>
   /// How loudly this failure is logged. A rejected API key is an <see cref="LogLevel.Error"/> so a
   /// misconfigured deployment is distinguishable from an ordinary gap in the translations.
   /// </summary>
   public LogLevel Level { get; }

   /// <summary>
   /// The phrase the log line ends with.
   /// </summary>
   public string Reason { get; }

   /// <summary>
   /// True when this failure is a statement about the service rather than about one request, and so
   /// should stop the client calling out for a while.
   /// </summary>
   public bool OpensSuppressionWindow { get; }

   private TranslationLookupFailure(LogLevel level, string reason, bool opensSuppressionWindow)
   {
      Level = level;
      Reason = reason;
      OpensSuppressionWindow = opensSuppressionWindow;
   }

   /// <summary>
   /// The lookup never left the process: a window opened by an earlier failure is still open.
   /// </summary>
   public static TranslationLookupFailure Suppressed { get; } =
      new(LogLevel.Warning, "the service is suppressed after a recent failure", opensSuppressionWindow: false);

   /// <summary>
   /// Classifies whatever the fetch threw. Only three things open the suppression window: a
   /// connection failure, the client's own timeout, and a 5xx. A response the service actually
   /// produced about one request never opens it, so a 401 and a 404 are answered and logged without
   /// suppressing the next lookup. Neither does a body the client cannot read: the service answered.
   /// </summary>
   public static TranslationLookupFailure Classify(Exception exception)
   {
      return exception switch {
         // The client's own timeout. The caller's own cancellation never reaches here.
         OperationCanceledException => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: true),

         // EnsureSuccessStatusCode records the status code it rejected.
         HttpRequestException { StatusCode: { } statusCode } => FromStatusCode(statusCode),

         // No status code means the request never got an answer at all.
         HttpRequestException => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: true),

         // An empty or undeserialisable body. The service answered, so the window stays shut.
         _ => new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: false)
      };
   }

   private static TranslationLookupFailure FromStatusCode(HttpStatusCode statusCode)
   {
      if (statusCode == HttpStatusCode.Unauthorized)
         return new TranslationLookupFailure(LogLevel.Error, CouldNotAnswer, opensSuppressionWindow: false);

      if (statusCode == HttpStatusCode.NotFound)
         return new TranslationLookupFailure(LogLevel.Warning, NoValueForThisKey, opensSuppressionWindow: false);

      return new TranslationLookupFailure(LogLevel.Warning, CouldNotAnswer, opensSuppressionWindow: (int)statusCode >= 500);
   }
}
