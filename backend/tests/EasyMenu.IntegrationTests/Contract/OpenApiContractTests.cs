using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Contract;

/// <summary>
/// Constitution II: the exposed API must match contracts/openapi.yaml at schema level
/// (operations, status codes, request/response schemas). See <see cref="OpenApiContractComparer"/> for the rules.
/// </summary>
public sealed class OpenApiContractTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Exposed_api_conforms_to_the_contract()
    {
        var contract = OpenApiDocuments.LoadContract();
        var generated = OpenApiDocuments.ParseJson(await Client.GetStringAsync("/openapi/v1.json"));

        var divergences = OpenApiContractComparer.Compare(contract, generated);

        divergences.Should().BeEmpty(
            "the API (/openapi/v1.json) must conform to contracts/openapi.yaml. {0} divergence(s):{1}{2}{1}",
            divergences.Count, Environment.NewLine, string.Join(Environment.NewLine, divergences.Select(d => "  - " + d)));
    }
}
