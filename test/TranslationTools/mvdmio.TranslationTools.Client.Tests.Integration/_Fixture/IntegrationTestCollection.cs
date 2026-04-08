using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Integration._Fixture;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection
{
   public const string Name = "TranslationTools Client Integration";
}
