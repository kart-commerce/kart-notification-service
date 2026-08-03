using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Kart.Notification.Infrastructure.ChannelAdapters;

/// <summary>
/// NOTIF-10 / design-decisions.md's Resilience Pattern decision: per-channel bulkhead isolation
/// (a bounded concurrency limit, separate per adapter instance) combined with a circuit breaker
/// per downstream provider, so one channel's outage never starves another channel's capacity.
/// One instance per <see cref="Channel"/> (each DI-registered as its own singleton, each with its
/// own <see cref="SemaphoreSlim"/> and <see cref="ResiliencePipeline"/> - true bulkhead isolation,
/// not a shared pool with per-call bookkeeping).
/// </summary>
public abstract class ResilientChannelDeliveryAdapterBase : IChannelDeliveryAdapter
{
    private readonly SemaphoreSlim _bulkhead;
    private readonly ResiliencePipeline _circuitBreaker;
    private readonly ILogger _logger;

    protected ResilientChannelDeliveryAdapterBase(int bulkheadCapacity, ILogger logger)
    {
        _bulkhead = new SemaphoreSlim(bulkheadCapacity, bulkheadCapacity);
        _logger = logger;
        _circuitBreaker = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 8,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder().Handle<ChannelSendFailedException>(),
            })
            .Build();
    }

    public abstract Channel Channel { get; }

    protected abstract Task<bool> TrySendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken);

    public async Task<ChannelDeliveryResult> SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken)
    {
        if (!await _bulkhead.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            return new ChannelDeliveryResult(ChannelDeliveryStatus.Failed, "Bulkhead capacity exhausted for this channel.");
        }

        try
        {
            await _circuitBreaker.ExecuteAsync(async ct =>
            {
                var sent = await TrySendAsync(context, ct);
                if (!sent)
                {
                    throw new ChannelSendFailedException($"{Channel} provider declined delivery for event {context.EventId}.");
                }
            }, cancellationToken);

            return ChannelDeliveryResult.Sent;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("{Channel} circuit breaker is open; failing fast for eventId={EventId}.", Channel, context.EventId);
            return new ChannelDeliveryResult(ChannelDeliveryStatus.CircuitOpen, $"{Channel} circuit breaker is open.");
        }
        catch (ChannelSendFailedException ex)
        {
            return new ChannelDeliveryResult(ChannelDeliveryStatus.Failed, ex.Message);
        }
        finally
        {
            _bulkhead.Release();
        }
    }

    private sealed class ChannelSendFailedException(string message) : Exception(message);
}
