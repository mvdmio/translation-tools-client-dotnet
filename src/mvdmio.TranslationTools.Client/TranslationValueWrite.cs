using System.Globalization;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// One value an app writes to the service with <see cref="ITranslationToolsClient.WriteAsync"/>.
/// </summary>
/// <param name="Translation">Origin and key of the translation to write.</param>
/// <param name="Locale">Locale of the value. The invariant culture writes the Default locale.</param>
/// <param name="Value">The text to store. An empty string clears the stored text. A null value is rejected.</param>
public sealed record TranslationValueWrite(TranslationRef Translation, CultureInfo Locale, string Value);
