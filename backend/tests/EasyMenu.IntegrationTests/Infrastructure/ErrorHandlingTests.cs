using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// Cross-cutting error handling and JSON conventions (research R10, T014), exercised through the test-only
/// <see cref="ErrorTestController"/>.
/// </summary>
public sealed class ErrorHandlingTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private HttpClient TestClient => _testClient ??= Factory.WithTestControllers.CreateClient();
    private HttpClient? _testClient;

    public override Task DisposeAsync()
    {
        _testClient?.Dispose();
        return base.DisposeAsync();
    }

    [Fact]
    public async Task Domain_exception_becomes_problem_details_with_its_code()
    {
        var problem = await ApiDriver.ReadProblemAsync(await TestClient.GetAsync("/__tests/errors/domain/x"));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("x");
        problem.BodyStatus.Should().Be(400);
    }

    [Theory]
    [InlineData("slug_taken", 409)]
    [InlineData("email_taken", 409)]
    [InlineData("category_not_empty", 409)]
    [InlineData("items_unavailable", 409)]
    [InlineData("invalid_status_transition", 409)]
    [InlineData("order_not_ready", 409)]
    [InlineData("not_found", 404)]
    [InlineData("category_not_found", 404)]
    [InlineData("order_not_found", 404)]
    [InlineData("establishment_not_found", 404)]
    [InlineData("payment_declined", 402)]
    [InlineData("invalid_slug", 400)]
    [InlineData("invalid_price", 400)]
    [InlineData("empty_cart", 400)]
    public async Task Domain_error_codes_map_to_the_contract_status(string code, int status)
    {
        var problem = await ApiDriver.ReadProblemAsync(await TestClient.GetAsync($"/__tests/errors/domain/{code}"));

        problem.Status.Should().Be(status, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be(code);
        problem.BodyStatus.Should().Be(status);
    }

    [Fact]
    public async Task Invalid_model_becomes_400_validation_failed_with_camel_case_errors()
    {
        var response = await TestClient.PostAsJsonAsync(
            "/__tests/errors/validate", new { firstName = (string?)null, quantity = 0 }, ApiDriver.Json);

        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Keys.Should().BeEquivalentTo(["firstName", "quantity"]);
        problem.Errors.Values.Should().OnlyContain(messages => messages.Length > 0);
    }

    [Fact]
    public async Task Malformed_json_becomes_400_validation_failed()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var problem = await ApiDriver.ReadProblemAsync(await TestClient.PostAsync("/__tests/errors/validate", content));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Not_found_exception_becomes_404_not_found()
    {
        var problem = await ApiDriver.ReadProblemAsync(await TestClient.GetAsync("/__tests/errors/not-found"));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Enums_are_snake_case_and_null_fields_are_omitted()
    {
        var response = await TestClient.GetAsync("/__tests/errors/serialization");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(body);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("ready_for_pickup");
        root.TryGetProperty("note", out _).Should().BeFalse("null fields are omitted: {0}", body);
        root.GetProperty("amountCents").ValueKind.Should().Be(JsonValueKind.Number);
        root.GetProperty("amountCents").GetInt64().Should().Be(1999);
    }

    [Fact]
    public async Task Snake_case_enums_are_accepted_on_input()
    {
        using var content = new StringContent(
            """{ "status": "awaiting_payment", "amountCents": 5 }""", System.Text.Encoding.UTF8, "application/json");

        var response = await TestClient.PostAsync("/__tests/errors/serialization", content);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(body);
        JsonDocument.Parse(body).RootElement.GetProperty("status").GetString().Should().Be("awaiting_payment");
    }

    [Fact]
    public async Task Amounts_sent_as_strings_are_rejected()
    {
        using var content = new StringContent(
            """{ "status": "awaiting_payment", "amountCents": "5" }""", System.Text.Encoding.UTF8, "application/json");

        var problem = await ApiDriver.ReadProblemAsync(await TestClient.PostAsync("/__tests/errors/serialization", content));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("validation_failed");
    }
}
