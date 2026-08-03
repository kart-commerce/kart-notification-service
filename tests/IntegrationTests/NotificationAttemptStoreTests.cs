using Kart.Notification.Domain.Enums;
using Kart.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kart.Notification.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class NotificationAttemptStoreTests(PostgresFixture fixture)
{
    [Fact]
    public async Task TryCreateAsync_returns_pending_when_no_preference_row_exists()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var outcome = await store.TryCreateAsync(eventId, Channel.Email, userId, "UserRegistered", CriticalityTier.Tier2, "account", CancellationToken.None);

        // Modeling Decision #8: absence of a NotificationPreference row defaults to opted-in.
        Assert.Equal(DeliveryOutcome.Pending, outcome);
    }

    [Fact]
    public async Task TryCreateAsync_returns_suppressed_when_the_channel_category_pair_is_opted_out()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var preferenceStore = new NotificationPreferenceStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await preferenceStore.UpsertAsync(userId, """{"Email": {"marketing": true}}""", appInstalled: false, CancellationToken.None);

        var outcome = await store.TryCreateAsync(eventId, Channel.Email, userId, "WishlistPriceAlertTriggered", CriticalityTier.Tier3, "marketing", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Suppressed, outcome);
    }

    [Fact]
    public async Task TryCreateAsync_is_idempotent_under_redelivery_of_the_same_event_id_and_channel()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await store.TryCreateAsync(eventId, Channel.Email, userId, "UserRegistered", CriticalityTier.Tier2, "account", CancellationToken.None);
        var redelivery = await store.TryCreateAsync(eventId, Channel.Email, userId, "UserRegistered", CriticalityTier.Tier2, "account", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Pending, first);
        Assert.Null(redelivery);
    }

    [Fact]
    public async Task IncrementAttemptAsync_returns_the_new_attempt_count()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await store.TryCreateAsync(eventId, Channel.Email, userId, "UserRegistered", CriticalityTier.Tier2, "account", CancellationToken.None);

        var count1 = await store.IncrementAttemptAsync(eventId, Channel.Email, CancellationToken.None);
        var count2 = await store.IncrementAttemptAsync(eventId, Channel.Email, CancellationToken.None);

        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
    }

    [Fact]
    public async Task MarkFailedAsync_sets_terminal_failed_status()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await store.TryCreateAsync(eventId, Channel.Email, userId, "WishlistPriceAlertTriggered", CriticalityTier.Tier3, "marketing", CancellationToken.None);
        await store.IncrementAttemptAsync(eventId, Channel.Email, CancellationToken.None);

        await store.MarkFailedAsync(eventId, Channel.Email, CancellationToken.None);

        var status = await dbContext.Database.SqlQuery<string>(
            $"""SELECT status AS "Value" FROM notification_attempts WHERE event_id = {eventId} AND channel = 'Email'""").SingleAsync();
        Assert.Equal("Failed", status);
    }

    [Fact]
    public async Task Illegal_status_transition_out_of_a_terminal_state_is_rejected_by_the_db_trigger()
    {
        await using var dbContext = fixture.CreateDbContext();
        var store = new NotificationAttemptStore(dbContext);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await store.TryCreateAsync(eventId, Channel.Email, userId, "UserRegistered", CriticalityTier.Tier2, "account", CancellationToken.None);
        await store.MarkSentAsync(eventId, Channel.Email, CancellationToken.None);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            dbContext.Database.ExecuteSqlAsync(
                $"UPDATE notification_attempts SET status = 'Pending' WHERE event_id = {eventId} AND channel = 'Email'"));
    }

    [Fact]
    public async Task Attempt_count_ceiling_check_constraint_rejects_a_count_above_the_tier_budget()
    {
        await using var dbContext = fixture.CreateDbContext();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Tier3's own ceiling is 2 - a direct insert at count 3 must violate the CHECK constraint.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            dbContext.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO notification_attempts (event_id, channel, user_id, triggering_event_type, criticality_tier, category, status, attempt_count)
                 VALUES ({eventId}, 'Email', {userId}, 'WishlistPriceAlertTriggered', 'Tier3', 'marketing', 'Pending', 3)
                 """));
    }
}
