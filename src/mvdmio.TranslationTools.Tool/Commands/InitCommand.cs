using mvdmio.TranslationTools.Tool.Configuration;
using System.CommandLine;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class InitCommand
{
   public static Command Create()
   {
      var command = new Command("init", "Initialize a .mvdmio-translations.yml configuration file in the current directory");

      command.SetAction(_ => {
         var currentDirectory = Directory.GetCurrentDirectory();
         var configPath = Path.Combine(currentDirectory, ToolConfiguration.CONFIG_FILE_NAME);

         if (File.Exists(configPath))
         {
            Console.Error.WriteLine($"Error: Configuration file already exists: {configPath}");
            return;
         }

         var config = new ToolConfiguration {
            ApiKey = "project-api-key",
            DefaultLocale = "en"
         };

         config.Save(currentDirectory);

         Console.WriteLine($"Created configuration file: {configPath}");
          Console.WriteLine();
          Console.WriteLine("Default settings:");
          Console.WriteLine($"  defaultLocale:  {config.DefaultLocale}");
          Console.WriteLine();
          Console.WriteLine("Edit the file to configure your project API key and default locale.");
          Console.WriteLine("All tool configuration is read from .mvdmio-translations.yml.");
       });

      return command;
   }
}
