using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationFileChangeTrigger.Notification;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace NotificationFileChangeTrigger;

internal static class HostConfig
{
    public static IHost Configure()
    {
        var hostBuilder = new HostBuilder();
        ConfigureServices(hostBuilder);
        ConfigureLogging(hostBuilder);
        return hostBuilder.Build();
    }

    private static void ConfigureServices(HostBuilder hostBuilder)
    {
        var settingsJson = JsonDocument.Parse(File.ReadAllText("appsettings.json"))
            .RootElement.GetProperty("settings").ToString();

        var settings = JsonSerializer.Deserialize<Settings>(settingsJson) ??
            throw new ArgumentException("Could not deserialize appsettings into settings.");

        hostBuilder.ConfigureServices((hostContext, services) =>
        {
            services.AddHostedService<NotificationFileChangeTriggerHost>();
            services.AddSingleton<Settings>(settings);
            services.AddSingleton<FileChangedSubscriber>();
        });
    }

    private static void ConfigureLogging(HostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((hostContext, services) =>
        {
            var loggingConfiguration = new ConfigurationBuilder()
               .AddEnvironmentVariables().Build();

            services.AddLogging(loggingBuilder =>
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(loggingConfiguration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();

                loggingBuilder.AddSerilog(logger, true);
            });
        });
    }
}
