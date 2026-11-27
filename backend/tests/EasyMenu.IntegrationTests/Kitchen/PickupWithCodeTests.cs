using System.Net;
using System.Text;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyMenu.IntegrationTests.Kitchen;

/// <summary>
/// PUT /api/kitchen/orders/{orderId}/pickup with <c>ConfirmPickupRequest { pickupCode }</c> (FR-027, FR-027a, SC-009).
/// Checks, in this order: missing body/property 400 validation_failed → 404 → 409 order_already_picked_up →
/// 409 order_not_ready → 400 pickup_code_required → 400 invalid_pickup_code. Trim + case-insensitive comparison.
/// </summary>
public sealed class PickupWithCodeTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Code = "K7P4";
    private const string OtherCode = "W2X3";

    private readonly List<HubConnection> _connections = [];

    public override async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private sealed record Scenario(RegisteredOwner Owner, string KitchenToken, KitchenOrderDto Order, KitchenOrderDto OtherOrder);

    /// <summary>Two orders of the same establishment with known codes; the first one is made ready unless told otherwise.</summary>
    private async Task<Scenario> ArrangeAsync(bool ready = true)
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        var kitchenToken = await Api.CreateKitchenTokenAsync(owner.EstablishmentId);
        await using (var host = Factory.WithPickupCodes(Code, OtherCode))
        {
            using var client = host.CreateClient();
            var request = new PlaceOrderRequest(null, [new PlaceOrderLine(menu.Caipirinha.Id, 1), new PlaceOrderLine(menu.Batata.Id, 1)]);
            await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(owner.Response.User.Establishment.Slug, request, client), "order");
            await ApiDriver.ReadSuccessAsync<PublicOrderDto>(await Api.PostOrderAsync(owner.Response.User.Establishment.Slug, request, client), "other");
        }

        var list = await Api.ListKitchenOrdersAsync(kitchenToken);
        var order = list.Single(o => o.PickupCode == Code);
        var other = list.Single(o => o.PickupCode == OtherCode);
        if (ready)
        {
            order = await Api.MakeReadyAsync(kitchenToken, order);
        }

        return new Scenario(owner, kitchenToken, order, other);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var problem = await ApiDriver.ReadProblemAsync(response);
        problem.Status.Should().Be(status, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be(code);
    }

    private async Task AssertStillReadyAndListedAsync(Scenario s)
    {
        var order = (await Api.ListKitchenOrdersAsync(s.KitchenToken)).SingleOrDefault(o => o.Id == s.Order.Id);
        order.Should().NotBeNull("a failed confirmation keeps the order in the panel");
        order!.Status.Should().Be("ready");
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    // ------------------------------------------------------------------ success

    [Theory]
    [InlineData("K7P4")]
    [InlineData(" k7p4 ")]
    [InlineData("k7P4")]
    public async Task Right_code_confirms_the_pickup(string provided)
    {
        var s = await ArrangeAsync();

        var response = await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, provided);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var order = await ApiDriver.ReadSuccessAsync<KitchenOrderDto>(response, "pickup");
        order.Id.Should().Be(s.Order.Id);
        order.Status.Should().Be("picked_up");
        (await Api.ListKitchenOrdersAsync(s.KitchenToken)).Select(o => o.Id).Should().NotContain(s.Order.Id);
    }

    // ------------------------------------------------------------------ 1. body / property missing

    [Fact]
    public async Task Missing_body_returns_400_validation_failed()
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupRawAsync(s.KitchenToken, s.Order.Id, content: null), 400, "validation_failed");
        await AssertStillReadyAndListedAsync(s);
    }

    [Fact]
    public async Task Body_without_pickup_code_returns_400_validation_failed()
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupRawAsync(s.KitchenToken, s.Order.Id, Json("{}")), 400, "validation_failed");
        await AssertStillReadyAndListedAsync(s);
    }

    [Fact]
    public async Task Pickup_code_longer_than_twenty_characters_returns_400_validation_failed()
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, new string('K', 21)), 400, "validation_failed");
    }

    [Fact]
    public async Task Missing_body_is_checked_before_the_order_exists()
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupRawAsync(s.KitchenToken, Guid.NewGuid(), Json("{}")), 400, "validation_failed");
    }

    // ------------------------------------------------------------------ 2. not found

    [Theory]
    [InlineData("K7P4")]
    [InlineData("")]
    [InlineData("XXXX")]
    public async Task Unknown_order_returns_404_before_any_code_check(string provided)
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, Guid.NewGuid(), provided), 404, "not_found");
    }

    // ------------------------------------------------------------------ 3. already picked up

    [Theory]
    [InlineData("K7P4")]
    [InlineData("XXXX")]
    [InlineData("")]
    public async Task Already_picked_up_returns_409_order_already_picked_up_whatever_the_code(string provided)
    {
        var s = await ArrangeAsync();
        (await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, Code)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, provided), 409, "order_already_picked_up");
    }

    // ------------------------------------------------------------------ 4. not ready

    [Theory]
    [InlineData("K7P4")]
    [InlineData("")]
    [InlineData("XXXX")]
    public async Task Not_ready_returns_409_order_not_ready_whatever_the_code(string provided)
    {
        var s = await ArrangeAsync(ready: false);

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, provided), 409, "order_not_ready");
        (await Api.ListKitchenOrdersAsync(s.KitchenToken)).Single(o => o.Id == s.Order.Id).Status.Should().Be("paid");
    }

    // ------------------------------------------------------------------ 5. blank code

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_code_returns_400_pickup_code_required(string provided)
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, provided), 400, "pickup_code_required");
        await AssertStillReadyAndListedAsync(s);
    }

    // ------------------------------------------------------------------ 6. wrong code

    [Theory]
    [InlineData("K7P5")]
    [InlineData("K7 P4")]
    [InlineData("K7P")]
    public async Task Wrong_code_returns_400_invalid_pickup_code(string provided)
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, provided), 400, "invalid_pickup_code");
        await AssertStillReadyAndListedAsync(s);
    }

    [Fact]
    public async Task Code_of_another_order_of_the_same_establishment_returns_400_invalid_pickup_code()
    {
        var s = await ArrangeAsync();

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, OtherCode), 400, "invalid_pickup_code");
        await AssertStillReadyAndListedAsync(s);
    }

    // ------------------------------------------------------------------ concurrency and events

    [Fact]
    public async Task Concurrent_right_confirmations_give_one_200_and_one_409_and_a_single_event()
    {
        var s = await ArrangeAsync();
        var connection = await Factory.ConnectKitchenHubAsync(s.KitchenToken);
        _connections.Add(connection);
        var pickedUpEvents = HubTestHelpers.RecordPayloads<OrderPickedUpEventDto>(connection, "OrderPickedUp");

        var responses = await Task.WhenAll(
            Api.PutPickupAsync(s.KitchenToken, s.Order.Id, Code),
            Api.PutPickupAsync(s.KitchenToken, s.Order.Id, " k7p4 "));

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        var summary = string.Join(" | ", responses.Select((r, i) => $"{(int)r.StatusCode} {bodies[i]}"));
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, summary);
        await AssertProblemAsync(responses.Single(r => r.StatusCode != HttpStatusCode.OK), 409, "order_already_picked_up");

        await Task.Delay(TimeSpan.FromSeconds(2));
        pickedUpEvents.Should().ContainSingle("OrderPickedUp is emitted exactly once: {0}", summary)
            .Which.Should().Be(new OrderPickedUpEventDto(s.Order.Id, Code));
    }

    [Fact]
    public async Task Failed_confirmations_emit_no_event()
    {
        var s = await ArrangeAsync();
        var connection = await Factory.ConnectKitchenHubAsync(s.KitchenToken);
        _connections.Add(connection);
        var received = HubTestHelpers.RecordEvents(connection, "OrderPickedUp", "OrderUpdated");

        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, "XXXX"), 400, "invalid_pickup_code");
        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.Order.Id, "  "), 400, "pickup_code_required");
        await AssertProblemAsync(await Api.PutPickupAsync(s.KitchenToken, s.OtherOrder.Id, OtherCode), 409, "order_not_ready");
        await Task.Delay(TimeSpan.FromSeconds(2));

        received.Should().BeEmpty("failed confirmations change nothing and notify nobody");
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Isolation_token_of_b_with_the_right_code_of_a_returns_404()
    {
        var a = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();
        var kitchenB = await Api.CreateKitchenTokenAsync(ownerB.EstablishmentId);

        await AssertProblemAsync(await Api.PutPickupAsync(kitchenB, a.Order.Id, Code), 404, "not_found");
        await AssertProblemAsync(await Api.PutPickupAsync(ownerB.Token, a.Order.Id, Code), 404, "not_found");
        await AssertStillReadyAndListedAsync(a);
    }
}
