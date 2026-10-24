using mvdmio.TranslationTools.Tool.Configuration;

namespace mvdmio.TranslationTools.Tool.Commands;

internal static class ApiKeyMessages
{
   /// <summary>
   /// The message the handlers report when neither the config file nor the environment variable holds an API key.
   /// </summary>
   public const string MISSING_API_KEY_MESSAGE = $"Error: No API key provided. Add apiKey to {ToolConfiguration.CONFIG_FILE_NAME} or set the {ToolConfiguration.API_KEY_ENVIRONMENT_VARIABLE} environment variable.";
}
