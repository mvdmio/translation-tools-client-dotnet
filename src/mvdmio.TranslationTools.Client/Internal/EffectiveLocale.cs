using System;
using System.Globalization;

namespace mvdmio.TranslationTools.Client.Internal;

/// <summary>
/// The locale a lookup actually runs against, after the client has replaced a locale it cannot use.
/// A caller may pass the invariant culture, whose name is empty; the effective locale is what the
/// client substitutes for it.
///
/// This is a type rather than a <see cref="string"/> because an unresolved locale name reaching a
/// request path is the bug the failure contract exists to fix: an empty name collapsed the path, the
/// service matched no route, and every lookup threw. Only <see cref="Resolve(CultureInfo, string)"/> builds one, so a raw
/// <see cref="CultureInfo.Name"/> cannot become a cache key or a URL segment by accident — the
/// compiler asks for a resolution first.
/// </summary>
internal readonly struct EffectiveLocale : IEquatable<EffectiveLocale>
{
   private readonly string? _name;

   /// <summary>
   /// The locale name the request path and the cache key are built from. Never blank.
   /// </summary>
   public string Name => _name ?? throw new InvalidOperationException($"An {nameof(EffectiveLocale)} must be built by {nameof(EffectiveLocale)}.{nameof(Resolve)}.");

   private EffectiveLocale(string name)
   {
      _name = name;
   }

   /// <summary>
   /// Resolves the locale a lookup runs against. A locale whose name is blank (the invariant
   /// culture) is replaced by the configured default. It does nothing else: it does not walk the
   /// culture parent chain, so a caller asking for <c>nl-BE</c> gets <c>nl-BE</c>. Which locales
   /// exist is the service's business.
   /// </summary>
   /// <param name="locale">The locale the caller named, or the thread's current UI culture.</param>
   /// <param name="defaultLocale">
   /// <see cref="TranslationToolsClientOptions.DefaultLocale"/>, already checked by
   /// <see cref="ValidateDefault"/> when the client was constructed.
   /// </param>
   public static EffectiveLocale Resolve(CultureInfo locale, string defaultLocale)
   {
      ArgumentNullException.ThrowIfNull(locale);

      return Resolve(locale.Name, defaultLocale);
   }

   /// <summary>
   /// Resolves a locale name the same way <see cref="Resolve(CultureInfo, string)"/> does, without
   /// requiring the name to be a runtime <see cref="CultureInfo"/>. Blank names become
   /// <paramref name="defaultLocale"/>.
   /// </summary>
   public static EffectiveLocale Resolve(string? localeName, string defaultLocale)
   {
      return new EffectiveLocale(string.IsNullOrWhiteSpace(localeName) ? defaultLocale : localeName);
   }

   /// <summary>
   /// Checks that the configured default is a locale the runtime recognises, so that a fallback
   /// which is itself broken fails when the client is constructed rather than one lookup at a time
   /// in production.
   /// </summary>
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

   /// <inheritdoc />
   public bool Equals(EffectiveLocale other)
   {
      return string.Equals(_name, other._name, StringComparison.Ordinal);
   }

   /// <inheritdoc />
   public override bool Equals(object? obj)
   {
      return obj is EffectiveLocale other && Equals(other);
   }

   /// <inheritdoc />
   public override int GetHashCode()
   {
      return _name is null ? 0 : StringComparer.Ordinal.GetHashCode(_name);
   }

   /// <inheritdoc />
   public override string ToString()
   {
      return Name;
   }
}
