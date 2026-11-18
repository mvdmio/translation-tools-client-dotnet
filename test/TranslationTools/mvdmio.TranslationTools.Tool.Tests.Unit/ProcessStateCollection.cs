using Xunit;

namespace mvdmio.TranslationTools.Tool.Tests.Unit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
   public const string Name = "Process-wide state";
}
