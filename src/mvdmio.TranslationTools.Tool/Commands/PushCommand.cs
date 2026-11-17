using mvdmio.TranslationTools.Tool.Push;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class PushCommand
{
   public static Command Create()
   {
      var handler = new PushHandler();
      var pruneOption = new Option<bool>("--prune")
      {
         Hidden = true
      };
      var command = new Command("push", "Push project translation keys to the API");
      command.Options.Add(pruneOption);

      command.SetAction(async (_, cancellationToken) =>
      {
         await handler.HandleAsync(cancellationToken);
      });

      return command;
   }
}
