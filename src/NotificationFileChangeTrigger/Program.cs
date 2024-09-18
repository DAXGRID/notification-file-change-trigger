using Microsoft.Extensions.Logging;

namespace NotificationFileChangeTrigger;

internal static class Program
{
    public static async Task Main()
    {
        var settings = AppSetting.Load<Settings>();
        var logger = LoggerFactory.Create(nameof(Program));
        using var cancellationTokenSource = new CancellationTokenSource();

        void CleanShutdown()
        {
            logger.LogInformation("Process is exiting.");
            cancellationTokenSource.Cancel();
        }

        // This handles if signal is send to the process.
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            CleanShutdown();
        };

        // This handles if user cancels the process.
        Console.CancelKeyPress += (sender, args) =>
        {
            CleanShutdown();
        };

        var notificationFileChangeTriggerHost = new NotificationFileChangeTriggerHost(
            LoggerFactory.Create(nameof(NotificationFileChangeTriggerHost)),
            new Notification.FileChangedSubscriber(settings),
            settings
        );

        try
        {
            await notificationFileChangeTriggerHost
                .StartAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical("{Exception}", ex);
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }
}
