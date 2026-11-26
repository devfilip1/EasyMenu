namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for API integration tests: provides an <see cref="HttpClient"/> bound to the in-memory API
/// and starts every test with an empty database.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(EasyMenuApiFactory factory) : IAsyncLifetime
{
    protected EasyMenuApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    public virtual Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
