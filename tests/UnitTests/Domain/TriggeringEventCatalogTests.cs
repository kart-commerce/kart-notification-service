using Kart.Notification.Domain.Catalog;
using Kart.Notification.Domain.Enums;
using Xunit;

namespace Kart.Notification.UnitTests.Domain;

public class TriggeringEventCatalogTests
{
    [Theory]
    [InlineData(TriggeringEventType.OrderCreated, CriticalityTier.Tier1)]
    [InlineData(TriggeringEventType.PaymentCompleted, CriticalityTier.Tier1)]
    [InlineData(TriggeringEventType.PaymentFailed, CriticalityTier.Tier1)]
    [InlineData(TriggeringEventType.RefundIssued, CriticalityTier.Tier1)]
    [InlineData(TriggeringEventType.OrderConfirmed, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.OrderCancelled, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.OrderCompensationTriggered, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.OrderDelivered, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.ShipmentDispatched, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.DeliveryStatusUpdated, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.UserRegistered, CriticalityTier.Tier2)]
    [InlineData(TriggeringEventType.WishlistPriceAlertTriggered, CriticalityTier.Tier3)]
    public void Get_returns_the_tier_catalogued_in_event_contract_md(string eventType, CriticalityTier expectedTier)
    {
        var definition = TriggeringEventCatalog.Get(eventType);

        Assert.Equal(expectedTier, definition.Tier);
    }

    [Theory]
    [InlineData(CriticalityTier.Tier1, 5)]
    [InlineData(CriticalityTier.Tier2, 3)]
    [InlineData(CriticalityTier.Tier3, 2)]
    public void MaxAttempts_matches_design_decisions_retry_ladder_depth(CriticalityTier tier, int expectedMaxAttempts)
    {
        Assert.Equal(expectedMaxAttempts, tier.MaxAttempts());
    }

    [Fact]
    public void IsPaged_is_true_only_for_tier1()
    {
        Assert.True(CriticalityTier.Tier1.IsPaged());
        Assert.False(CriticalityTier.Tier2.IsPaged());
        Assert.False(CriticalityTier.Tier3.IsPaged());
    }

    [Theory]
    [InlineData(TriggeringEventType.PaymentCompleted)]
    [InlineData(TriggeringEventType.PaymentFailed)]
    [InlineData(TriggeringEventType.RefundIssued)]
    [InlineData(TriggeringEventType.ShipmentDispatched)]
    [InlineData(TriggeringEventType.DeliveryStatusUpdated)]
    public void CandidateChannelsExcludingPush_includes_sms_for_time_critical_events(string eventType)
    {
        var definition = TriggeringEventCatalog.Get(eventType);

        Assert.Contains(Channel.Sms, definition.CandidateChannelsExcludingPush());
    }

    [Theory]
    [InlineData(TriggeringEventType.OrderCreated)]
    [InlineData(TriggeringEventType.OrderConfirmed)]
    [InlineData(TriggeringEventType.UserRegistered)]
    [InlineData(TriggeringEventType.WishlistPriceAlertTriggered)]
    public void CandidateChannelsExcludingPush_excludes_sms_for_non_time_critical_events(string eventType)
    {
        var definition = TriggeringEventCatalog.Get(eventType);

        Assert.DoesNotContain(Channel.Sms, definition.CandidateChannelsExcludingPush());
    }

    [Fact]
    public void CandidateChannelsExcludingPush_always_includes_email()
    {
        foreach (var eventType in new[]
                 {
                     TriggeringEventType.OrderCreated, TriggeringEventType.PaymentCompleted, TriggeringEventType.UserRegistered,
                     TriggeringEventType.WishlistPriceAlertTriggered,
                 })
        {
            Assert.Contains(Channel.Email, TriggeringEventCatalog.Get(eventType).CandidateChannelsExcludingPush());
        }
    }

    [Fact]
    public void Get_throws_for_an_unrecognized_event_type()
    {
        Assert.Throws<KeyNotFoundException>(() => TriggeringEventCatalog.Get("SomethingNotInAdr0003"));
    }

    [Theory]
    [InlineData(TriggeringEventType.OrderCreated, TriggeringEventType.OrderConfirmed, TriggeringEventType.OrderCancelled, TriggeringEventType.OrderCompensationTriggered)]
    public void TryGet_returns_true_for_every_ADR_0003_scoped_event(params string[] eventTypes)
    {
        foreach (var eventType in eventTypes)
        {
            Assert.True(TriggeringEventCatalog.TryGet(eventType, out var definition));
            Assert.NotNull(definition);
        }
    }
}
