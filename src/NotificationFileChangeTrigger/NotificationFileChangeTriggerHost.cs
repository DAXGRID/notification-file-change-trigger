using Microsoft.Extensions.Logging;
using NotificationFileChangeTrigger.FileServer;
using NotificationFileChangeTrigger.Notification;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace NotificationFileChangeTrigger;

internal sealed class NotificationFileChangeTriggerHost
{
    private readonly ILogger _logger;
    private readonly FileChangedSubscriber _fileChangedSubscriber;
    private readonly Settings _settings;

    public NotificationFileChangeTriggerHost(
        ILogger logger,
        FileChangedSubscriber fileChangedSubscriber,
        Settings settings)
    {
        _logger = logger;
        _fileChangedSubscriber = fileChangedSubscriber;
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

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
                            cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                }
            }
        }

        // After we have pushed initial load, we subscribe for future changes.
        var subscribeFileChangesTask = _fileChangedSubscriber
            .Subscribe(fileChangedCh.Writer, cancellationTokenSource);

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var fileChange in fileChangedCh.Reader.ReadAllAsync(cancellationTokenSource.Token))
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

                    var triggerResult = Trigger.Execute(
                        _settings.TriggerCommand,
                        downloadedFileOutputPath,
                        (string msg) => _logger.LogInformation("{InformationMessage}", msg),
                        (string msg) => _logger.LogError("{ErrorMessage}", msg)
                    );

                    if (!triggerResult)
                    {
                        // The message is written to the log, in the trigger result, so we just
                        // want the process to die here.
                        throw new TriggerException($"The trigger failed for the file: '{downloadedFileOutputPath}'.");
                    }

                    if (_settings.RemoveFileOnFileServerWhenCompleted)
                    {
                        _logger.LogInformation(
                            "Removing file on file server {FileName}.", fileChange.FullPath);
                        await httpFileServer
                            .DeleteResource(fileChange.FileName, fileChange.DirectoryName)
                            .ConfigureAwait(false);
                    }

                    _logger.LogInformation("Finished processing {FileChange}.", fileChange.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Unhandled {Exception}, stopping service.", ex);
                    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }, cancellationTokenSource.Token);

        _logger.LogInformation("The subscriber and consumer has now been started.");
        await Task.WhenAll(subscribeFileChangesTask, consumeTask).ConfigureAwait(false);
    }
}
