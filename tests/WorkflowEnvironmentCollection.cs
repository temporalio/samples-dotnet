#pragma warning disable CA1711 // We can suffix "Collection" in xUnit

namespace TemporalioSamples.Tests;

using Xunit;

[CollectionDefinition("Environment")]
public class WorkflowEnvironmentCollection : ICollectionFixture<WorkflowEnvironment>
{
}
