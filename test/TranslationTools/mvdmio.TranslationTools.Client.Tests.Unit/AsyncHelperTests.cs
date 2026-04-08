using AwesomeAssertions;
using mvdmio.TranslationTools.Client.Internal;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

public class AsyncHelperTests
{
   [Fact]
   public void RunSync_ShouldReturnAsyncResult()
   {
      var result = AsyncHelper.RunSync(async () =>
      {
         await Task.Yield();
         return 42;
      });

      result.Should().Be(42);
   }

   [Fact]
   public void RunSync_ShouldPropagateExceptions()
   {
      var act = () => AsyncHelper.RunSync(async () =>
      {
         await Task.Yield();
         throw new InvalidOperationException("boom");
      });

      act.Should().Throw<InvalidOperationException>().WithMessage("boom");
   }
}
