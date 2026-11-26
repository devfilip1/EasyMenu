namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// All integration test classes share a single API host + PostgreSQL container.
/// Inherit from <see cref="IntegrationTestBase"/> (already in this collection).
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<EasyMenuApiFactory>
{
    public const string Name = "Api";
}
