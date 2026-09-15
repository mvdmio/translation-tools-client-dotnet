using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Fluent builder for placeholder substitution against a dynamically referenced translation key.
/// Bindings are validated at render time (string-keyed path: <c>knownSet = null</c>, so every token that is
/// neither bound nor a registered global degrades to a raw token + warning, or throws when configured).
/// </summary>
[PublicAPI]
public sealed class PlaceholderBuilder
{
   private readonly TranslationRef _translation;
   private readonly string? _defaultValue;
   private readonly Dictionary<string, string?> _bindings = new(StringComparer.Ordinal);
   private CultureInfo? _locale;

   internal PlaceholderBuilder(TranslationRef translation, string? defaultValue)
   {
      _translation = translation;
      _defaultValue = defaultValue;
   }

   /// <summary>
   /// Bind a placeholder value. A binding shadows a global of the same name for this render.
   /// </summary>
   public PlaceholderBuilder SetPlaceholder(string name, string? value)
   {
      if (string.IsNullOrEmpty(name))
         throw new ArgumentException("Placeholder name must be non-empty.", nameof(name));

      _bindings[name] = value;
      return this;
   }

   /// <summary>
   /// Render for a specific locale instead of <see cref="CultureInfo.CurrentUICulture"/>.
   /// </summary>
   public PlaceholderBuilder ForLocale(CultureInfo locale)
   {
      _locale = locale ?? throw new ArgumentNullException(nameof(locale));
      return this;
   }

   /// <summary>
   /// Fetch the translation and apply substitution (knownSet = null).
   /// </summary>
   public string Render()
   {
      return _locale is null
         ? Translations.GetWithPlaceholders(_translation, _defaultValue, localeValues: null, _bindings, knownSet: null)
         : Translations.GetWithPlaceholders(_translation, _locale, _defaultValue, localeValues: null, _bindings, knownSet: null);
   }

   /// <summary>
   /// Fetch the translation asynchronously and apply substitution (knownSet = null).
   /// </summary>
   public Task<string> RenderAsync(CancellationToken cancellationToken = default)
   {
      return _locale is null
         ? Translations.GetWithPlaceholdersAsync(_translation, _defaultValue, localeValues: null, _bindings, knownSet: null, cancellationToken)
         : Translations.GetWithPlaceholdersAsync(_translation, _locale, _defaultValue, localeValues: null, _bindings, knownSet: null, cancellationToken);
   }
}
