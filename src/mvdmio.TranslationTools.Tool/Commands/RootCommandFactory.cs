using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class RootCommandFactory
{
   /// <summary>
   ///    Builds the root command of the tool with the init, pull, and push subcommands.
   /// </summary>
   /// <returns>The root command with all subcommands registered.</returns>
   public static RootCommand Create()
   {
      var rootCommand = new RootCommand("TranslationTools manifest tool");
      rootCommand.Subcommands.Add(InitCommand.Create());
      rootCommand.Subcommands.Add(PullCommand.Create());
      rootCommand.Subcommands.Add(PushCommand.Create());
      return rootCommand;
   }
}
