using System.Diagnostics;

namespace mvdmio.TranslationTools.Client.Tests.Integration._Fixture;

internal static class Eventually
{
   public static async Task<T> AssertAsync<T>(Func<Task<T>> action, Func<T, bool> predicate, TimeSpan timeout, string because)
   {
      var stopwatch = Stopwatch.StartNew();
      T last = default!;

      while (stopwatch.Elapsed < timeout)
      {
         last = await action();
         if (predicate(last))
            return last;

         await Task.Delay(TimeSpan.FromMilliseconds(25));
      }

      throw new Xunit.Sdk.XunitException($"Timed out after {timeout.TotalMilliseconds} ms waiting for condition: {because}. Last value: {last}");
   }
}
