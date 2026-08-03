using Kart.Notification.Infrastructure.Persistence;
using Xunit;

namespace Kart.Notification.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class NotificationPreferenceStoreTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetAppInstalledAsync_defaults_to_false_when_no_row_exists()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationPreferenceStore(dbContext);

        var appInstalled = await store.GetAppInstalledAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(appInstalled);
    }

    [Fact]
    public async Task UpsertAsync_then_GetAppInstalledAsync_reflects_the_stored_value()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationPreferenceStore(dbContext);
        var userId = Guid.NewGuid();

        await store.UpsertAsync(userId, "{}", appInstalled: true, CancellationToken.None);

        Assert.True(await store.GetAppInstalledAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertAsync_replaces_the_whole_map_on_a_second_call_full_replace_not_merge()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationPreferenceStore(dbContext);
        var userId = Guid.NewGuid();

        await store.UpsertAsync(userId, """{"Email": {"marketing": true}}""", appInstalled: true, CancellationToken.None);
        await store.UpsertAsync(userId, """{"SMS": {"order-updates": true}}""", appInstalled: false, CancellationToken.None);

        // The second upsert must fully replace the first, not merge - Modeling Decision #7.
        Assert.False(await store.GetAppInstalledAsync(userId, CancellationToken.None));
    }
}
