using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Infrastructure.Auditing;
using Kart.Notification.Infrastructure.ChannelAdapters;
using Kart.Notification.Infrastructure.Messaging;
using Kart.Notification.Infrastructure.Messaging.Dispatchers;
using Kart.Notification.Infrastructure.Persistence;
using Kart.Shared.Auditing;
using Kart.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Kart.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("NotificationDb")));

        services.AddScoped<INotificationAttemptStore, NotificationAttemptStore>();
        services.AddScoped<INotificationPreferenceStore, NotificationPreferenceStore>();
        services.AddScoped<IUserIdResolutionService, LookupIndexStore>();

        // The user's own explicit ask for audit logging - a real writer, not the shared package's
        // NullAuditLogWriter default (kart-order-service's own precedent as the first service to
        // register one).
        services.AddKartAuditing<EfAuditLogWriter>();

        services.AddSingleton<IChannelDeliveryAdapter, EmailChannelDeliveryAdapter>();
        services.AddSingleton<IChannelDeliveryAdapter, SmsChannelDeliveryAdapter>();
        services.AddSingleton<IChannelDeliveryAdapter, PushChannelDeliveryAdapter>();
        services.AddSingleton<IChannelDeliveryAdapterFactory, ChannelDeliveryAdapterFactory>();

        // contracts/message-bus-manifest.json is the single source of truth for this service's
        // entire RabbitMQ topology - see contracts/README.md for why its JSON shape differs from
        // kart-platform's own docs-repo copy of this file.
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(
                o => string.IsNullOrEmpty(o.UserName) == string.IsNullOrEmpty(o.Password),
                "RabbitMq:UserName and RabbitMq:Password must either both be set or both be left unset.")
            .ValidateOnStart();
        services.AddKartMessageBusManifest(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.ManifestPath);
        services.AddKartRabbitMqConnectionFactory(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqConnectionSettings(options.HostName, options.Port, options.UserName, options.Password);
        });
        services.AddKartRabbitMqTopologyStartup();

        services.AddSingleton<INotificationSentPublisher, NotificationSentPublisher>();

        AddConsumerHostedServices(services);

        return services;
    }

    /// <summary>
    /// One `NotificationQueueConsumerHostedService` per manifest queue, each wired to its own
    /// dispatcher and its own namespaced retry-count header (per-service namespacing convention -
    /// see `Kart.Shared.Messaging`'s own remarks on why this must never collide across services).
    /// </summary>
    private static void AddConsumerHostedServices(IServiceCollection services)
    {
        AddConsumer(services, "notification.order-events.queue", OrderEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.payment-events.queue", PaymentEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.wishlist-events.queue", WishlistEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.identity-events.queue", IdentityEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.shipping-events.queue", ShippingEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.tracking-events.queue", TrackingEventsDispatcher.DispatchAsync);
        AddConsumer(services, "notification.user-events.queue", UserEventsDispatcher.DispatchAsync);
    }

    private static void AddConsumer(IServiceCollection services, string queueName, NotificationQueueDispatcher dispatch)
    {
        services.AddSingleton<IHostedService>(sp => new NotificationQueueConsumerHostedService(
            sp.GetRequiredService<IConnectionFactory>(),
            sp.GetRequiredService<MessageBusManifest>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<NotificationQueueConsumerHostedService>>(),
            queueName,
            "x-notification-service-retry-count",
            dispatch));
    }
}
