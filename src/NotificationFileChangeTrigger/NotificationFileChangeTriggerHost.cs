using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationFileChangeTrigger;

internal sealed class NotificationFileChangeTriggerHost : BackgroundService
{
    private readonly ILogger<NotificationFileChangeTriggerHost> _logger;

    public NotificationFileChangeTriggerHost(
        ILogger<NotificationFileChangeTriggerHost> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {HostName}.",
            nameof(NotificationFileChangeTriggerHost));

        return Task.CompletedTask;
    }
}
