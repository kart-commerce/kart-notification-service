using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Infrastructure.ChannelAdapters;

public sealed class ChannelDeliveryAdapterFactory(IEnumerable<IChannelDeliveryAdapter> adapters) : IChannelDeliveryAdapterFactory
{
    private readonly IReadOnlyDictionary<Channel, IChannelDeliveryAdapter> _byChannel = adapters.ToDictionary(a => a.Channel);

    public IChannelDeliveryAdapter Resolve(Channel channel) =>
        _byChannel.TryGetValue(channel, out var adapter)
            ? adapter
            : throw new KeyNotFoundException($"No IChannelDeliveryAdapter registered for channel '{channel}'.");
}
