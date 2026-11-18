using System;

namespace mvdmio.TranslationTools.Client.Placeholders;

internal sealed class AmbientScope
{
   private static Func<IServiceProvider?>? _accessor;

   public static void SetAccessor(Func<IServiceProvider?> accessor)
   {
      _accessor = accessor;
   }

   public static void Reset()
   {
      _accessor = null;
   }

   public static IServiceProvider? Current => _accessor?.Invoke();
}
