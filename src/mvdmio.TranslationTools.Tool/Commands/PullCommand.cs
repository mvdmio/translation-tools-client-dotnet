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
         Description = "Delete local .resx files and entries missing from the remote project"
      };

      var command = new Command("pull", "Pull translations from the API into local .resx files");
      command.Options.Add(pruneOption);

      command.SetAction(async (parseResult, cancellationToken) =>
      {
         await handler.HandleAsync(parseResult.GetValue(pruneOption), cancellationToken);
      });

      return command;
   }
}
