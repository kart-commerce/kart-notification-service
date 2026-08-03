using Kart.Notification.Api;
using Kart.Notification.Api.HealthChecks;
using Kart.Notification.Application;
using Kart.Notification.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
// Notification is on the STANDARD sampling tier (design-decisions.md's Observability decision) -
// not one of the four Order Saga 100%-trace-coverage services - so no extra sampler override.
builder.AddKartObservability("kart-notification-service");

// /health/live: process is up, no dependency check. /health/ready: this service's job depends on
// Postgres being reachable AND migrated (a connectable-but-unmigrated database, e.g. tonight's
// missing notification_attempts partitions, is not "ready").
builder.Services.AddHealthChecks()
    .AddCheck<NotificationDbHealthCheck>("notification-db", tags: ["ready"]);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// kart-conventions.md Error Handling section: the single global exception handler + ProblemDetails
// factory, wired once via the shared package - no local try/catch for translation anywhere in
// this service's handler/domain code, even though it has no public API to serve error responses
// to (this still guards any incidental exception surfaced through health checks/framework code).
builder.Services.AddKartErrorHandling();

var app = builder.Build();

await StartupConnectivityChecks.RunAsync(app);

app.UseKartErrorHandling();

// Per-HTTP-request Information log (method/path/status/elapsed) - free RED-style access log for
// the health/metrics endpoints this service does expose.
app.UseSerilogRequestLogging();

// Prometheus scrape target (observability-standards.md's mandatory /metrics).
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// No business endpoints are mapped - api-contract.yaml is deliberately empty. Every outcome this
// service produces is driven by the 7 consumer hosted services registered in AddInfrastructure.

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program;
