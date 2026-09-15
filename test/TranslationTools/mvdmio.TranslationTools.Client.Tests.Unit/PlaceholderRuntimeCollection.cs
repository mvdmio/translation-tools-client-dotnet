using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// Serializes tests that mutate process-global placeholder/runtime state (<c>Translations</c> static client,
/// <c>PlaceholderRuntime</c>, <c>AmbientScope</c>) so they do not race under parallel execution.
/// </summary>
[CollectionDefinition("PlaceholderRuntime")]
public sealed class PlaceholderRuntimeCollection;
