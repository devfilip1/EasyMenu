namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for API integration tests: provides an <see cref="HttpClient"/> bound to the in-memory API,
/// an <see cref="ApiDriver"/> for building scenarios, and starts every test with an empty database and a
/// fresh fake clock (<see cref="EasyMenuApiFactory.Clock"/>).
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(EasyMenuApiFactory factory) : IAsyncLifetime
{
    protected EasyMenuApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected ApiDriver Api { get; } = new(factory);

    public virtual Task InitializeAsync()
    {
        Factory.ResetClock();
        return Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        Api.Dispose();
        return Task.CompletedTask;
    }
}
