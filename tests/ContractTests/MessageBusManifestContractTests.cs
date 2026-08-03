using Kart.Shared.Messaging;
using Xunit;

namespace Kart.Notification.ContractTests;

/// <summary>
/// Verifies contracts/message-bus-manifest.json (copied to this test project's output directory)
/// both loads correctly against `Kart.Shared.Messaging`'s actual schema and matches
/// event-contract.md's approved topology (messaging-contract.md's own index) - the two facts
/// contracts/README.md documents as the reason this file's JSON shape was translated from
/// kart-platform's docs-repo copy.
/// </summary>
public class MessageBusManifestContractTests
{
    private static readonly MessageBusManifest Manifest = MessageBusManifestLoader.Load(
        Path.Combine(AppContext.BaseDirectory, "message-bus-manifest.json"));

    [Fact]
    public void Manifest_declares_this_services_own_exchange_and_dlx()
    {
        Assert.Contains(Manifest.Exchanges, e => e.Name == "notification.exchange" && e.Type == "topic" && e.Durable);
        Assert.Contains(Manifest.Exchanges, e => e.Name == "notification.dlx" && e.Type == "topic" && e.Durable);
    }

    [Theory]
    [InlineData("order.exchange")]
    [InlineData("payment.exchange")]
    [InlineData("wishlist.exchange")]
    [InlineData("identity.exchange")]
    [InlineData("shipping.exchange")]
    [InlineData("tracking.exchange")]
    [InlineData("user.exchange")]
    public void Manifest_declares_all_seven_external_producer_exchanges(string exchangeName)
    {
        Assert.Contains(Manifest.ExternalExchanges, e => e.Name == exchangeName);
    }

    [Fact]
    public void Manifest_publishes_NotificationSent_with_the_approved_routing_key()
    {
        Assert.Equal("notification.exchange", Manifest.ExchangeFor("NotificationSent"));
        Assert.Equal("notification.notification.sent", Manifest.RoutingKeyFor("NotificationSent"));
    }

    [Theory]
    [InlineData("notification.order-events.queue", "order.exchange", "order.#")]
    [InlineData("notification.payment-events.queue", "payment.exchange", "payment.#")]
    [InlineData("notification.wishlist-events.queue", "wishlist.exchange", "wishlist.price-alert.triggered")]
    [InlineData("notification.identity-events.queue", "identity.exchange", "identity.user.registered")]
    [InlineData("notification.shipping-events.queue", "shipping.exchange", "shipping.shipment.dispatched")]
    [InlineData("notification.tracking-events.queue", "tracking.exchange", "tracking.delivery-status.updated")]
    [InlineData("notification.user-events.queue", "user.exchange", "user.notification-preference-updated")]
    public void Each_queue_binds_to_its_producers_own_exchange_never_a_shared_one(string queueName, string exchange, string routingKey)
    {
        var queue = Manifest.GetQueue(queueName);

        Assert.Contains(queue.Bindings, b => b.Exchange == exchange && b.RoutingKey == routingKey);
    }

    [Theory]
    [InlineData("notification.order-events.queue", "notification.order-events.dlq")]
    [InlineData("notification.payment-events.queue", "notification.payment-events.dlq")]
    [InlineData("notification.wishlist-events.queue", "notification.wishlist-events.dlq")]
    [InlineData("notification.identity-events.queue", "notification.identity-events.dlq")]
    [InlineData("notification.shipping-events.queue", "notification.shipping-events.dlq")]
    [InlineData("notification.tracking-events.queue", "notification.tracking-events.dlq")]
    [InlineData("notification.user-events.queue", "notification.user-events.dlq")]
    public void Every_queue_has_its_own_dedicated_dlq_never_shared(string queueName, string expectedDlqRoutingKey)
    {
        var queue = Manifest.GetQueue(queueName);

        Assert.NotNull(queue.DeadLetter);
        Assert.Equal("notification.dlx", queue.DeadLetter!.Exchange);
        Assert.Equal(expectedDlqRoutingKey, queue.DeadLetter.RoutingKey);

        // The DLQ names must be unique per queue - never accidentally reused across two queues.
        var dlqNames = Manifest.DeadLetterQueues.Select(d => d.Name).ToList();
        Assert.Equal(dlqNames.Count, dlqNames.Distinct().Count());
    }

    [Theory]
    [InlineData("notification.order-events.queue", 5)] // carries OrderCreated (Tier1, 5 attempts) - the deepest tier on this queue
    [InlineData("notification.payment-events.queue", 5)] // all Tier1
    [InlineData("notification.identity-events.queue", 3)] // Tier2
    [InlineData("notification.shipping-events.queue", 3)] // Tier2
    [InlineData("notification.tracking-events.queue", 3)] // Tier2
    [InlineData("notification.wishlist-events.queue", 2)] // Tier3
    [InlineData("notification.user-events.queue", 2)] // Tier3
    public void Each_queues_retry_ladder_has_at_least_as_many_rungs_as_its_deepest_carried_tier_needs(string queueName, int minimumRungs)
    {
        var queue = Manifest.GetQueue(queueName);

        Assert.NotNull(queue.RetryLadder);
        Assert.True(queue.RetryLadder!.Tiers.Count >= minimumRungs,
            $"{queueName} has {queue.RetryLadder.Tiers.Count} retry rungs, needs at least {minimumRungs}.");
        Assert.Equal(queueName, queue.RetryLadder.RequeueTo);
    }

    [Fact]
    public void Exactly_seven_queues_are_declared_matching_the_platforms_broadest_fan_in_topology()
    {
        Assert.Equal(7, Manifest.Queues.Count);
        Assert.Equal(7, Manifest.DeadLetterQueues.Count);
    }

    /// <summary>
    /// Regression guard: every consumed event's routing key is 3 segments
    /// (`service.entity.action`, naming-conventions.md) - a single-word topic wildcard segment
    /// (`*`) only matches exactly one word, so `order.*` would never match `order.order.created`.
    /// The wildcard binding on a multi-tier queue must be `#` (zero-or-more words), not `*` - this
    /// exact bug shipped in kart-platform's own illustrative manifest.json and was only caught by
    /// a live RabbitMQ end-to-end test, not by schema validation.
    /// </summary>
    [Theory]
    [InlineData("notification.order-events.queue")]
    [InlineData("notification.payment-events.queue")]
    public void Multi_tier_queue_bindings_use_the_hash_wildcard_not_the_single_segment_star(string queueName)
    {
        var queue = Manifest.GetQueue(queueName);

        Assert.All(queue.Bindings, binding => Assert.EndsWith(".#", binding.RoutingKey));
    }
}
