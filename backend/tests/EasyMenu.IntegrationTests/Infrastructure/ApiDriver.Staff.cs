using System.Net.Http.Json;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Scenario helpers for US5 (staff and owner order history).</summary>
public sealed partial class ApiDriver
{
    /// <summary><c>POST /api/staff</c> (raw response).</summary>
    public Task<HttpResponseMessage> PostStaffAsync(string? token, CreateStaffMemberRequest request) =>
        CreateClient(token).PostAsJsonAsync("/api/staff", request, Json);

    /// <summary>Creates a Kitchen member through the API (fails the test if not 2xx).</summary>
    public async Task<(StaffMemberDto Member, string Password)> CreateStaffAsync(string ownerToken, string? name = null, string? email = null)
    {
        var n = Interlocked.Increment(ref _sequence);
        var request = new CreateStaffMemberRequest(
            name ?? $"Cozinha {n}",
            email ?? $"staff-{n}-{Guid.NewGuid():N}"[..20] + "@easymenu.test",
            DefaultPassword);
        var member = await ReadSuccessAsync<StaffMemberDto>(await PostStaffAsync(ownerToken, request), "create staff");
        return (member, request.Password);
    }

    /// <summary><c>GET /api/staff</c> (fails the test if not 2xx).</summary>
    public async Task<IReadOnlyList<StaffMemberDto>> ListStaffAsync(string ownerToken) =>
        await ReadSuccessAsync<List<StaffMemberDto>>(await CreateClient(ownerToken).GetAsync("/api/staff"), "list staff");

    /// <summary><c>PUT /api/staff/{id}/deactivate</c> (raw response).</summary>
    public Task<HttpResponseMessage> DeactivateStaffAsync(string? token, Guid id) =>
        CreateClient(token).PutAsync($"/api/staff/{id}/deactivate", content: null);

    /// <summary><c>GET /api/orders</c> with an optional query string (raw response).</summary>
    public Task<HttpResponseMessage> GetOrdersResponseAsync(string? token, string query = "") =>
        CreateClient(token).GetAsync($"/api/orders{query}");

    /// <summary><c>GET /api/orders</c> (fails the test if not 2xx).</summary>
    public async Task<OrderSummaryPageDto> ListOrdersAsync(string ownerToken, string query = "") =>
        await ReadSuccessAsync<OrderSummaryPageDto>(await GetOrdersResponseAsync(ownerToken, query), "list orders");
}

public sealed record CreateStaffMemberRequest(string Name, string Email, string Password);

public sealed record StaffMemberDto(Guid Id, string Name, string Email, bool IsActive, DateTimeOffset CreatedAt);

public sealed record OrderSummaryDto(Guid Id, string PickupCode, string? CustomerName, string Status, long TotalCents, DateTimeOffset CreatedAt);

public sealed record OrderSummaryPageDto(IReadOnlyList<OrderSummaryDto> Items, int Page, int PageSize, int TotalCount);

public sealed record OrderDetailLineDto(string Name, long UnitPriceCents, int Quantity, long SubtotalCents, string Station);

public sealed record OrderPartStatusDto(string Station, string Status);

public sealed record OrderDetailDto(
    Guid Id,
    string PickupCode,
    string? CustomerName,
    string Status,
    long TotalCents,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PickedUpAt,
    IReadOnlyList<OrderDetailLineDto> Items,
    IReadOnlyList<OrderPartStatusDto> Parts);
