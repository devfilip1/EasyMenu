using EasyMenu.Application.Abstractions;
using EasyMenu.Domain.Orders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Returns the queued codes in order, then falls back to random valid codes.</summary>
public sealed class ScriptedPickupCodeGenerator(params string[] codes) : IPickupCodeGenerator
{
    private readonly Queue<string> _codes = new(codes);
    private readonly object _lock = new();

    public string Generate()
    {
        lock (_lock)
        {
            return _codes.Count > 0 ? _codes.Dequeue() : PickupCode.Generate();
        }
    }
}

public static class ScriptedPickupCodeHost
{
    /// <summary>
    /// A host (same database, settings and clock) whose <see cref="IPickupCodeGenerator"/> returns
    /// <paramref name="codes"/> in order. Dispose it after use.
    /// </summary>
    public static WebApplicationFactory<Program> WithPickupCodes(this EasyMenuApiFactory factory, params string[] codes) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPickupCodeGenerator>();
            services.AddSingleton<IPickupCodeGenerator>(new ScriptedPickupCodeGenerator(codes));
        }));
}
