using System;

namespace mvdmio.TranslationTools.Client.Placeholders;

/// <summary>
/// Provides the <see cref="IServiceProvider"/> for the current ambient request scope so that a static
/// <c>Translations.*</c> call can resolve the request-scoped global placeholder configuration.
/// </summary>
/// <remarks>
/// When ASP.NET Core is present, this is backed by <c>IHttpContextAccessor.HttpContext.RequestServices</c>.
/// Outside any request scope the accessor returns null and globals degrade to raw tokens (+warn) by default.
/// </remarks>
internal sealed class AmbientScope
{
   private static Func<IServiceProvider?>? _accessor;

   /// <summary>
   /// Register the ambient scope accessor. Called once at startup.
   /// </summary>
   public static void SetAccessor(Func<IServiceProvider?> accessor)
   {
      _accessor = accessor;
   }

   /// <summary>
   /// Reset the accessor. Intended for tests.
   /// </summary>
   public static void Reset()
   {
      _accessor = null;
   }

   /// <summary>
   /// The service provider for the current ambient scope, or null when none is available.
   /// </summary>
   public static IServiceProvider? Current => _accessor?.Invoke();
}
