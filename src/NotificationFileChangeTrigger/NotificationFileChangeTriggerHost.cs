using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationFileChangeTrigger.FileServer;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace NotificationFileChangeTrigger;

internal sealed class NotificationFileChangeTriggerHost : BackgroundService
{
    private readonly ILogger<NotificationFileChangeTriggerHost> _logger;
    private readonly FileChangedSubscriber _fileChangedSubscriber;
    private readonly Settings _settings;

    public NotificationFileChangeTriggerHost(
        ILogger<NotificationFileChangeTriggerHost> logger,
        FileChangedSubscriber fileChangedSubscriber,
        Settings settings)
    {
        _logger = logger;
        _fileChangedSubscriber = fileChangedSubscriber;
        _settings = settings;
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

        var fileMatchesRegex = _settings.FileNotificationMatches
            .Select(x => new Regex(x));

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var fileChanged in fileChangedCh.Reader.ReadAllAsync(stoppingToken))
            {
                if (!fileMatchesRegex.Any(x => x.IsMatch(fileChanged.FullPath)))
                {
                    continue;
                }

                _logger.LogInformation("Processing {FileName}.", fileChanged.FullPath);
            }
        }, stoppingToken);

        _logger.LogInformation("Starting subscriber and consumer.");
        await Task.WhenAll(subscribeFileChangesTask, consumeTask).ConfigureAwait(false);
    }
}
