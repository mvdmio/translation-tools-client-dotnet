using mvdmio.TranslationTools.Tool.Configuration;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class InitCommand
{
   public static Command Create()
   {
      var command = new Command("init", "Initialize a .mvdmio-translations.yml configuration file in the current directory");

      command.SetAction(_ => Run(Directory.GetCurrentDirectory(), Console.Out, Console.Error));

      return command;
   }

   internal static void Run(string directory, TextWriter output, TextWriter error)
   {
      var configPath = Path.Combine(directory, ToolConfiguration.CONFIG_FILE_NAME);

      if (File.Exists(configPath))
      {
         error.WriteLine($"Error: Configuration file already exists: {configPath}");
         return;
      }

      var config = new ToolConfiguration
      {
         DefaultLocale = "en"
      };

      config.Save(directory);

      output.WriteLine($"Created configuration file: {configPath}");
      output.WriteLine();
      output.WriteLine("Default settings:");
      output.WriteLine($"  defaultLocale:  {config.DefaultLocale}");
      output.WriteLine();
      output.WriteLine($"Provide your project API key through the {ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE} environment variable, or add apiKey to the file.");
      output.WriteLine("All other tool configuration is read from .mvdmio-translations.yml.");
   }
}
