using System;
using System.Globalization;

namespace mvdmio.TranslationTools.Client.Internal;

internal readonly struct EffectiveLocale : IEquatable<EffectiveLocale>
{
   private readonly string? _name;

   public string Name => _name ?? throw new InvalidOperationException($"An {nameof(EffectiveLocale)} must be built by {nameof(EffectiveLocale)}.{nameof(Resolve)}.");

   private EffectiveLocale(string name)
   {
      _name = name;
   }

   public static EffectiveLocale Resolve(CultureInfo locale, string defaultLocale)
   {
      ArgumentNullException.ThrowIfNull(locale);

      return Resolve(locale.Name, defaultLocale);
   }

   public static EffectiveLocale Resolve(string? localeName, string defaultLocale)
   {
      return new EffectiveLocale(string.IsNullOrWhiteSpace(localeName) ? defaultLocale : localeName);
   }

   public static void ValidateDefault(string? defaultLocale, string parameterName)
   {
      if (string.IsNullOrWhiteSpace(defaultLocale))
         throw new ArgumentException("DefaultLocale is required.", parameterName);

      try
      {
         _ = new CultureInfo(defaultLocale);
      }
      catch (CultureNotFoundException exception)
      {
         throw new ArgumentException($"DefaultLocale '{defaultLocale}' is not a recognised locale.", parameterName, exception);
      }
   }

   public bool Equals(EffectiveLocale other)
   {
      return string.Equals(_name, other._name, StringComparison.Ordinal);
   }

   public override bool Equals(object? obj)
   {
      return obj is EffectiveLocale other && Equals(other);
   }

   public override int GetHashCode()
   {
      return _name is null ? 0 : StringComparer.Ordinal.GetHashCode(_name);
   }

   public override string ToString()
   {
      return Name;
   }
}
