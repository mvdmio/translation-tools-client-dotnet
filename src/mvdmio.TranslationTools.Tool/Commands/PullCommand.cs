using mvdmio.TranslationTools.Tool.Pull;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class PullCommand
{
   public static Command Create()
   {
      var handler = new PullHandler();
      var pruneOption = new Option<bool>("--prune")
      {
         Hidden = true
      };

      var command = new Command("pull", "Pull translations from the API into local .resx files");
      command.Options.Add(pruneOption);

      command.SetAction(async (_, cancellationToken) =>
      {
         await handler.HandleAsync(cancellationToken);
      });

      return command;
   }
}
