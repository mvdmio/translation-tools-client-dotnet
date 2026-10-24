using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit;

/// <summary>
/// Guards process-wide state (the current directory and environment variables). Tests that change that state join this
/// collection, so xunit does not run them in parallel with each other or with other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
   public const string Name = "Process-wide state";
}
