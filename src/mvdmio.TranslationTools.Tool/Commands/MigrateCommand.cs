using mvdmio.TranslationTools.Tool.Migrate;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class MigrateCommand
{
   public static Command Create()
   {
      var handler = new MigrateHandler();
      var command = new Command("migrate", "Import .resx translation files into TranslationTools and regenerate the manifest");

      command.SetAction(async (_, cancellationToken) => {
         await handler.HandleAsync(cancellationToken);
      });

      return command;
   }
}
