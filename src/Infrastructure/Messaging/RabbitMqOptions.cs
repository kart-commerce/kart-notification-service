using System.ComponentModel.DataAnnotations;

namespace Kart.Notification.Infrastructure.Messaging;

/// <summary>
/// Only connection details the manifest doesn't cover - every topology name (exchanges, queues,
/// bindings, DLQs, retry tiers) is deliberately absent from C#, living solely in
/// contracts/message-bus-manifest.json.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    [Required]
    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
