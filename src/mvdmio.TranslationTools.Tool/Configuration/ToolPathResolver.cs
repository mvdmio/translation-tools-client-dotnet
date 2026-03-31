namespace mvdmio.TranslationTools.Tool.Configuration;

internal static class ToolPathResolver
{
   public static string GetOutputPath(ToolConfiguration config, ToolProjectContext projectContext)
   {
      if (!string.IsNullOrWhiteSpace(config.Output))
         return Path.GetFullPath(Path.Combine(projectContext.ProjectDirectory, config.Output));

      return Path.GetFullPath(Path.Combine(projectContext.ProjectDirectory, $"{config.ClassName}.cs"));
   }

   public static string GetOutputPath(ToolConfiguration config)
   {
      if (!string.IsNullOrWhiteSpace(config.Output))
         return Path.GetFullPath(Path.Combine(config.ConfigDirectory, config.Output));

      return Path.GetFullPath(Path.Combine(config.ConfigDirectory, $"{config.ClassName}.cs"));
   }
}
