using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using Kart.Notification.Domain.Enums;
using Kart.Shared.Auditing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Kart.Notification.UnitTests.Application;

public class ProcessNotificationTriggerCommandHandlerTests
{
    private readonly INotificationAttemptStore _attemptStore = Substitute.For<INotificationAttemptStore>();
    private readonly INotificationPreferenceStore _preferenceStore = Substitute.For<INotificationPreferenceStore>();
    private readonly IChannelDeliveryAdapterFactory _adapterFactory = Substitute.For<IChannelDeliveryAdapterFactory>();
    private readonly INotificationSentPublisher _publisher = Substitute.For<INotificationSentPublisher>();
    private readonly IAuditLogWriter _auditLogWriter = Substitute.For<IAuditLogWriter>();

    private ProcessNotificationTriggerCommandHandler CreateHandler() => new(
        _attemptStore, _preferenceStore, _adapterFactory, _publisher, _auditLogWriter,
        Substitute.For<ILogger<ProcessNotificationTriggerCommandHandler>>());

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Suppressed_outcome_publishes_suppressed_and_never_attempts_delivery()
    {
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);
        _attemptStore.TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>())
            .Returns(DeliveryOutcome.Suppressed);

        var result = await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.UserRegistered, 0), CancellationToken.None);

        await _publisher.Received(1).PublishAsync(UserId, Channel.Email, DeliveryOutcome.Suppressed, Arg.Any<CancellationToken>());
        _adapterFactory.DidNotReceive().Resolve(Arg.Any<Channel>());
        Assert.False(result.NeedsRetry);
        Assert.False(result.AnyExhaustedThisPass);
    }

    [Fact]
    public async Task Redelivery_of_an_already_handled_channel_is_a_full_no_op()
    {
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);
        _attemptStore.TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>())
            .Returns((DeliveryOutcome?)null);

        var result = await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.UserRegistered, 0), CancellationToken.None);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default, default, default, default);
        _adapterFactory.DidNotReceive().Resolve(Arg.Any<Channel>());
        Assert.False(result.NeedsRetry);
        Assert.False(result.AnyExhaustedThisPass);
    }

    [Fact]
    public async Task Successful_delivery_marks_sent_and_publishes_sent()
    {
        var adapter = Substitute.For<IChannelDeliveryAdapter>();
        adapter.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>()).Returns(ChannelDeliveryResult.Sent);
        _adapterFactory.Resolve(Channel.Email).Returns(adapter);
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);
        _attemptStore.TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>())
            .Returns(DeliveryOutcome.Pending);

        var result = await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.UserRegistered, 0), CancellationToken.None);

        await _attemptStore.Received(1).MarkSentAsync(EventId, Channel.Email, Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(UserId, Channel.Email, DeliveryOutcome.Sent, Arg.Any<CancellationToken>());
        Assert.False(result.NeedsRetry);
        Assert.False(result.AnyExhaustedThisPass);
    }

    [Fact]
    public async Task Failed_delivery_below_tier_budget_increments_attempt_and_signals_retry()
    {
        // Tier2 (UserRegistered) allows 3 attempts - a failure bringing the count to 1 is still within budget.
        var adapter = Substitute.For<IChannelDeliveryAdapter>();
        adapter.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelDeliveryResult(ChannelDeliveryStatus.Failed, "provider timeout"));
        _adapterFactory.Resolve(Channel.Email).Returns(adapter);
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);
        _attemptStore.TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>())
            .Returns(DeliveryOutcome.Pending);
        _attemptStore.IncrementAttemptAsync(EventId, Channel.Email, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.UserRegistered, 0), CancellationToken.None);

        await _attemptStore.DidNotReceive().MarkFailedAsync(Arg.Any<Guid>(), Arg.Any<Channel>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default, default, default, default);
        Assert.True(result.NeedsRetry);
        Assert.False(result.AnyExhaustedThisPass);
    }

    [Fact]
    public async Task Failed_delivery_exhausting_tier_budget_marks_failed_and_publishes_failed()
    {
        // Tier3 (WishlistPriceAlertTriggered) allows only 2 attempts.
        var adapter = Substitute.For<IChannelDeliveryAdapter>();
        adapter.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelDeliveryResult(ChannelDeliveryStatus.Failed, "provider down"));
        _adapterFactory.Resolve(Channel.Email).Returns(adapter);
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);
        _attemptStore.TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.WishlistPriceAlertTriggered, CriticalityTier.Tier3, NotificationCategory.Marketing, Arg.Any<CancellationToken>())
            .Returns(DeliveryOutcome.Pending);
        _attemptStore.IncrementAttemptAsync(EventId, Channel.Email, Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.WishlistPriceAlertTriggered, 1), CancellationToken.None);

        await _attemptStore.Received(1).MarkFailedAsync(EventId, Channel.Email, Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(UserId, Channel.Email, DeliveryOutcome.Failed, Arg.Any<CancellationToken>());
        Assert.False(result.NeedsRetry);
        Assert.True(result.AnyExhaustedThisPass);
    }

    [Fact]
    public async Task AppInstalled_adds_push_as_a_candidate_channel()
    {
        _preferenceStore.GetAppInstalledAsync(UserId, Arg.Any<CancellationToken>()).Returns(true);
        _attemptStore.TryCreateAsync(Arg.Any<Guid>(), Arg.Any<Channel>(), UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>())
            .Returns((DeliveryOutcome?)null); // treat every channel as a no-op redelivery to isolate the channel-selection behavior

        await CreateHandler().Handle(new ProcessNotificationTriggerCommand(EventId, UserId, TriggeringEventType.UserRegistered, 0), CancellationToken.None);

        await _attemptStore.Received(1).TryCreateAsync(EventId, Channel.Email, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>());
        await _attemptStore.Received(1).TryCreateAsync(EventId, Channel.Push, UserId, TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, Arg.Any<CancellationToken>());
    }
}
