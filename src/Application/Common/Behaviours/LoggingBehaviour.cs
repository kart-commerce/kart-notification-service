using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Notification.Application.Common.Behaviours;

/// <summary>
/// observability-standards.md: every command gets a structured Information log on completion,
/// tagged with its own name and duration. Exceptions are intentionally left unlogged here and
/// rethrown as-is - logged once, at the true boundary (the Api layer's global exception handler
/// wired via `Kart.Shared.ErrorHandling`), not duplicated at every pipeline layer.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        logger.LogInformation(
            "{RequestName} completed in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
