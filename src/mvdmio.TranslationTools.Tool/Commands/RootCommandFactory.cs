using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class RootCommandFactory
{
   public static RootCommand Create()
   {
      var rootCommand = new RootCommand("TranslationTools manifest tool");
      rootCommand.Subcommands.Add(InitCommand.Create());
      rootCommand.Subcommands.Add(PullCommand.Create());
      rootCommand.Subcommands.Add(PushCommand.Create());
      return rootCommand;
   }
}
