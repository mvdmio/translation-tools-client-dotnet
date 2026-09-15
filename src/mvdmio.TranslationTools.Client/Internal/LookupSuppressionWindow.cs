using System;
using System.Threading;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// Stops the client calling a service it has just found to be unreachable or broken. A connection
/// failure, a timeout, or a 5xx opens the window; while it is open, every single-key lookup answers
/// from its local fallback without calling out, and a whole-locale lookup throws without calling
/// out.
///
/// The window is process-wide for one client instance rather than per locale or per key, because
/// what opens it is a statement about the service rather than about one request. It is tracked
/// against the injected <see cref="TimeProvider"/> so a fake clock can drive it in tests. Nothing
/// probes the service in the background: the window simply expires, and the first lookup after that
/// is a live call that either succeeds or opens a new window.
///
/// Neither the length nor a way to disable it is exposed on
/// <see cref="TranslationToolsClientOptions"/>.
/// </summary>
internal sealed class LookupSuppressionWindow
{
   private static readonly TimeSpan _length = TimeSpan.FromMinutes(1);

   private readonly TimeProvider _timeProvider;

   /// <summary>
   /// UTC ticks at which the current window ends, or 0 when no window is open. Read and written
   /// with <see cref="Interlocked"/> since lookups can run concurrently.
   /// </summary>
   private long _openUntilUtcTicks;

   public LookupSuppressionWindow(TimeProvider timeProvider)
   {
      _timeProvider = timeProvider;
   }

   /// <summary>
   /// True while a window opened by a recent failure has not yet expired.
   /// </summary>
   public bool IsOpen
   {
      get
      {
         var until = Interlocked.Read(ref _openUntilUtcTicks);
         return until != 0 && _timeProvider.GetUtcNow().UtcTicks < until;
      }
   }

   /// <summary>
   /// Opens, or re-opens, the window from now.
   /// </summary>
   public void Open()
   {
      Interlocked.Exchange(ref _openUntilUtcTicks, _timeProvider.GetUtcNow().UtcTicks + _length.Ticks);
   }
}
