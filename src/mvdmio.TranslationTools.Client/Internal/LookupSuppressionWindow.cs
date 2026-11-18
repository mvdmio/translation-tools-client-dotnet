using System;
using System.Threading;

namespace mvdmio.TranslationTools.Client.Internal;

internal sealed class LookupSuppressionWindow
{
   private static readonly TimeSpan _length = TimeSpan.FromMinutes(1);

   private readonly TimeProvider _timeProvider;

   private long _openUntilUtcTicks;

   public LookupSuppressionWindow(TimeProvider timeProvider)
   {
      _timeProvider = timeProvider;
   }

   public bool IsOpen
   {
      get
      {
         var until = Interlocked.Read(ref _openUntilUtcTicks);
         return until != 0 && _timeProvider.GetUtcNow().UtcTicks < until;
      }
   }

   public void Open()
   {
      Interlocked.Exchange(ref _openUntilUtcTicks, _timeProvider.GetUtcNow().UtcTicks + _length.Ticks);
   }
}
