using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using NotificationClient = OpenFTTH.NotificationClient.Client;

namespace NotificationFileChangeTrigger.FileServer;

internal sealed class FileChangedSubscriber : IDisposable
{
    private readonly Settings _settings;
    private readonly NotificationClient _notificationClient;

    public FileChangedSubscriber(Settings settings)
    {
        _settings = settings;

        var ipAddress = Dns.GetHostEntry(settings.NotificationServer.Domain).AddressList
            .First(x => x.AddressFamily == AddressFamily.InterNetwork);

        _notificationClient = new NotificationClient(
            ipAddress,
            settings.NotificationServer.Port);
    }

    public async Task Subscribe(
        ChannelWriter<FileChangedEvent> output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationCh = _notificationClient.Connect();

            var notifications = notificationCh
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false);

            await foreach (var notification in notifications)
            {
                if (string.CompareOrdinal(notification.Type, "FileChangedEvent") == 0)
                {
                    var fileChangedEvent = JsonSerializer
                        .Deserialize<FileChangedEvent>(notification.Body);

                    if (fileChangedEvent is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not deserialize {nameof(FileChangedEvent)}");
                    }

                    await output.WriteAsync(fileChangedEvent, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception)
        {
            // If an exception happens, we want to close the connection
            // and mark the channel as completed, so the consumers know
            // can stop consuming from it.
            Dispose();
            output.Complete();
            throw;
        }
    }

    public void Dispose()
    {
        _notificationClient.Dispose();
    }
}
