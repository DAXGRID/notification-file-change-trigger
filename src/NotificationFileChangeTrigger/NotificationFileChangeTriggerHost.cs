using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationFileChangeTrigger.FileServer;

namespace NotificationFileChangeTrigger;

internal sealed class NotificationFileChangeTriggerHost : BackgroundService
{
    private readonly ILogger<NotificationFileChangeTriggerHost> _logger;
    private readonly FileChangedSubscriber _fileChangedSubscriber;

    public NotificationFileChangeTriggerHost(
        ILogger<NotificationFileChangeTriggerHost> logger,
        FileChangedSubscriber fileChangedSubscriber)
    {
        _logger = logger;
        _fileChangedSubscriber = fileChangedSubscriber;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {Host}.",
            nameof(NotificationFileChangeTriggerHost));

        var fileChangedCh = Channel.CreateUnbounded<FileChangedEvent>();

        // After we have pushed initial load, we subscribe for future changes.
        var subscribeFileChangesTask = _fileChangedSubscriber
            .Subscribe(fileChangedCh.Writer, stoppingToken);

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var fileChanged in fileChangedCh.Reader.ReadAllAsync(stoppingToken))
            {
                _logger.LogInformation("{FileName}", fileChanged.FullPath);
            }
        }, stoppingToken);

        _logger.LogInformation("Starting subscriber and consumer.");
        await Task.WhenAll(subscribeFileChangesTask, consumeTask).ConfigureAwait(false);
    }
}
