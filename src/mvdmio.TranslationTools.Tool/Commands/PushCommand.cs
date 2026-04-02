using mvdmio.TranslationTools.Tool.Push;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class PushCommand
{
   public static Command Create()
   {
      var handler = new PushHandler();
      var command = new Command("push", "Push project .resx translations to the API");

      command.SetAction(async (_, cancellationToken) =>
      {
         await handler.HandleAsync(cancellationToken);
      });

      return command;
   }
}
