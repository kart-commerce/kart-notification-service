using Kart.Notification.Infrastructure.Persistence;
using Xunit;

namespace Kart.Notification.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class LookupIndexStoreTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ResolveUserIdByOrderIdAsync_returns_null_before_the_seeding_event_is_consumed()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new LookupIndexStore(dbContext);

        var userId = await store.ResolveUserIdByOrderIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(userId);
    }

    [Fact]
    public async Task SeedOrderUserIndexAsync_then_resolve_returns_the_seeded_user_id()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new LookupIndexStore(dbContext);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await store.SeedOrderUserIndexAsync(orderId, userId, CancellationToken.None);
        var resolved = await store.ResolveUserIdByOrderIdAsync(orderId, CancellationToken.None);

        Assert.Equal(userId, resolved);
    }

    [Fact]
    public async Task SeedOrderUserIndexAsync_is_idempotent_on_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new LookupIndexStore(dbContext);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await store.SeedOrderUserIndexAsync(orderId, userId, CancellationToken.None);
        await store.SeedOrderUserIndexAsync(orderId, Guid.NewGuid(), CancellationToken.None); // a redelivery with a "different" payload should still no-op

        var resolved = await store.ResolveUserIdByOrderIdAsync(orderId, CancellationToken.None);
        Assert.Equal(userId, resolved);
    }

    [Fact]
    public async Task DeliveryStatusUpdated_two_hop_chain_resolves_via_tracking_and_order_indexes()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new LookupIndexStore(dbContext);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingId = $"TRACK-{Guid.NewGuid():N}";

        await store.SeedOrderUserIndexAsync(orderId, userId, CancellationToken.None);
        await store.SeedTrackingOrderIndexAsync(trackingId, orderId, CancellationToken.None);

        var resolved = await store.ResolveUserIdByTrackingIdAsync(trackingId, CancellationToken.None);

        Assert.Equal(userId, resolved);
    }

    [Fact]
    public async Task ResolveUserIdByTrackingIdAsync_returns_null_when_the_tracking_id_hasnt_been_seeded()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new LookupIndexStore(dbContext);

        var resolved = await store.ResolveUserIdByTrackingIdAsync("TRACK-UNKNOWN", CancellationToken.None);

        Assert.Null(resolved);
    }
}
