using mvdmio.TranslationTools.Tool.Commands;
using System.CommandLine;

var rootCommand = new RootCommand("TranslationTools manifest tool");
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(PullCommand.Create());
rootCommand.Subcommands.Add(PushCommand.Create());

return rootCommand.Parse(args).Invoke();
