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

        using var httpClientHandler = new HttpClientHandler
        {
            // The file-server might return redirects,
            // we do not want to follow the redirects.
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = true,
        };

        using var httpClient = new HttpClient(httpClientHandler);

        var httpFileServer = new HttpFileServer(
            httpClient,
            _settings.FileServer.Username,
            _settings.FileServer.Password,
            new Uri(_settings.FileServer.Uri));

        var fileChangedCh = Channel.CreateUnbounded<FileChangedEvent>();

        var fileMatchesRegex = _settings.FileNotificationMatches
            .Select(x => new Regex(x))
            .ToArray();

        foreach (var initialLoadDirPath in _settings.InitialLoadDirectories)
        {
            var initialLoadFiles = await httpFileServer
                .ListFiles(initialLoadDirPath)
                .ConfigureAwait(false);

            foreach (var initialLoadFile in initialLoadFiles)
            {
                if (fileMatchesRegex.Any(x => x.IsMatch(initialLoadFile.FullPath)))
                {
                    _logger.LogInformation(
                        "Pushing initial load {FileName}.",
                        initialLoadFile.FullPath);

                    await fileChangedCh.Writer
                        .WriteAsync(
                            new FileChangedEvent(initialLoadFile.FullPath),
                            stoppingToken)
                        .ConfigureAwait(false);
                }
            }
        }

        // After we have pushed initial load, we subscribe for future changes.
        var subscribeFileChangesTask = _fileChangedSubscriber
            .Subscribe(fileChangedCh.Writer, stoppingToken);

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var fileChange in fileChangedCh.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    if (!fileMatchesRegex.Any(x => x.IsMatch(fileChange.FullPath)))
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Received file change. Starting downloading {AbsoluteUri}.",
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
                        throw new TriggerException(
                            $"StdOut: {triggerResult.message}, StdErr: {triggerResult.errorMessage}");
                    }

                    if (_settings.RemoveFileOnFileServerWhenCompleted)
                    {
                        _logger.LogInformation(
                            "Removing file on file server {FileName}.", fileChange.FullPath);
                        await httpFileServer
                            .DeleteResource(fileChange.FileName, fileChange.DirectoryName)
                            .ConfigureAwait(false);
                    }

                    _logger.LogInformation(
                        "Finished processing {FileChange}. {TriggerOutput}",
                        fileChange.FileName,
                        triggerResult.message);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Unhandled {Exception}, stopping service.", ex);
                    fileChangedCh.Writer.TryComplete(ex);
                    throw;
                }
            }
        }, stoppingToken);

        _logger.LogInformation("Starting subscriber and consumer.");
        await Task.WhenAll(subscribeFileChangesTask, consumeTask).ConfigureAwait(false);
    }
}
