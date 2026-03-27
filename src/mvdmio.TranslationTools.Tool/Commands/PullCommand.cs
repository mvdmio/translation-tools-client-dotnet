using mvdmio.TranslationTools.Tool.Pull;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class PullCommand
{
   public static Command Create()
   {
      var handler = new PullHandler();
      var overwriteOption = new Option<bool>("--overwrite") {
         Description = "Overwrite existing manifest values instead of merging with them"
      };

      var command = new Command("pull", "Pull translations from the API and generate a manifest file");
      command.Options.Add(overwriteOption);

      command.SetAction(async (parseResult, cancellationToken) => {
         await handler.HandleAsync(parseResult.GetValue(overwriteOption), cancellationToken);
      });

      return command;
   }
}
