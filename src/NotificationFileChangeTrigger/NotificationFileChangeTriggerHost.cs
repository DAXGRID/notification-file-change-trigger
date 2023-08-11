using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationFileChangeTrigger.FileServer;
using NotificationFileChangeTrigger.Notification;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace NotificationFileChangeTrigger;

internal sealed class NotificationFileChangeTriggerHost : BackgroundService
{
    private readonly ILogger<NotificationFileChangeTriggerHost> _logger;
    private readonly FileChangedSubscriber _fileChangedSubscriber;
    private readonly Settings _settings;
    private readonly HttpClient _httpClient;

    public NotificationFileChangeTriggerHost(
        ILogger<NotificationFileChangeTriggerHost> logger,
        FileChangedSubscriber fileChangedSubscriber,
        Settings settings,
        HttpClient httpClient)
    {
        _logger = logger;
        _fileChangedSubscriber = fileChangedSubscriber;
        _settings = settings;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {Host}.",
            nameof(NotificationFileChangeTriggerHost));

        var httpFileServer = new HttpFileServer(
            _httpClient,
            _settings.FileServer.Username,
            _settings.FileServer.Password,
            new Uri(_settings.FileServer.Uri));

        var fileChangedCh = Channel.CreateUnbounded<FileChangedEvent>();

        // After we have pushed initial load, we subscribe for future changes.
        var subscribeFileChangesTask = _fileChangedSubscriber
            .Subscribe(fileChangedCh.Writer, stoppingToken);

        var fileMatchesRegex = _settings.FileNotificationMatches
            .Select(x => new Regex(x))
            .ToArray();

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var fileChange in fileChangedCh.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Received file change {FileFullPath}", fileChange.FullPath);
                    if (!fileMatchesRegex.Any(x => x.IsMatch(fileChange.FullPath)))
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Downloading {AbsoluteUri}.",
                        fileChange.FullPath);

                    var fileByteAsyncEnumerable = httpFileServer
                        .DownloadFile(fileChange.FullPath)
                        .ConfigureAwait(false);

                    var downloadedFileOutputPath = $"{_settings.OutputDirectoryPath}{fileChange.FileName}";

                    using var fileStream = new FileStream(
                        downloadedFileOutputPath,
                        FileMode.Create,
                        FileAccess.Write);

                    await foreach (var buffer in fileByteAsyncEnumerable)
                    {
                        await fileStream.WriteAsync(buffer).ConfigureAwait(false);
                    }

                    await fileStream.FlushAsync().ConfigureAwait(false);
                    _logger.LogInformation(
                        "Finished downloading {FileName} to {OutputFullPath}.",
                        fileChange.FullPath,
                        downloadedFileOutputPath);

                    _logger.LogInformation(
                        "Executing the following command: {Command}",
                        _settings.TriggerCommand);

                    var triggerResult = Trigger.Execute(_settings.TriggerCommand, downloadedFileOutputPath);

                    if (!triggerResult.success)
                    {
                        throw new TriggerException(triggerResult.message);
                    }

                    _logger.LogInformation(
                        "Finished processing {FileChange}. {TriggerOutput}",
                        fileChange.FileName,
                        triggerResult.message);
                }
                catch (TriggerException ex)
                {
                    _logger.LogCritical("{Exception}", ex);
                }
            }
        }, stoppingToken);

        _logger.LogInformation("Starting subscriber and consumer.");
        await Task.WhenAll(subscribeFileChangesTask, consumeTask).ConfigureAwait(false);
    }
}
