using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationFileChangeTrigger;

internal static class Program
{
    public static async Task Main()
    {
        using var host = HostConfig.Configure();

        var loggerFactory = host.Services.GetService<ILoggerFactory>();
        var logger = loggerFactory!.CreateLogger(nameof(Program));

        try
        {
            await host.RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical("{Exception}", ex);
            throw;
        }
    }
}
