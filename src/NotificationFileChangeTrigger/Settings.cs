using System.Text.Json.Serialization;

namespace NotificationFileChangeTrigger;

internal sealed record FileServerSettings
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; }

    [JsonPropertyName("password")]
    public string Password { get; init; }

    [JsonConstructor]
    public FileServerSettings(
        string uri,
        string username,
        string password)
    {
        Uri = uri;
        Username = username;
        Password = password;
    }
}

internal sealed record NotificationServerSettings
{
    [JsonPropertyName("domain")]
    public string Domain { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonConstructor]
    public NotificationServerSettings(
        string domain,
        int port)
    {
        Domain = domain;
        Port = port;
    }
}

internal sealed record Settings
{
    [JsonPropertyName("notificationServer")]
    public NotificationServerSettings NotificationServer { get; init; }

    [JsonPropertyName("fileServer")]
    public FileServerSettings FileServer { get; init; }

    [JsonPropertyName("fileNotificationMatches")]
    public IReadOnlyList<string> FileNotificationMatches { get; init; }

    [JsonPropertyName("outputDirectoryPath")]
    public string OutputDirectoryPath { get; init; }

    [JsonPropertyName("triggerCommmand")]
    public string TriggerCommand { get; init; }

    [JsonPropertyName("removeFileOnFileServerWhenCompleted")]
    public bool RemoveFileOnFileServerWhenCompleted { get; init; }

    [JsonPropertyName("initialLoadDirectories")]
    public IReadOnlyList<string> InitialLoadDirectories { get; init; }

    [JsonConstructor]
    public Settings(
        NotificationServerSettings notificationServer,
        FileServerSettings fileServer,
        IReadOnlyList<string> fileNotificationMatches,
        string outputDirectoryPath,
        string triggerCommand,
        bool removeFileOnFileServerWhenCompleted,
        IReadOnlyList<string> initialLoadDirectories)
    {
        NotificationServer = notificationServer;
        FileServer = fileServer;
        FileNotificationMatches = fileNotificationMatches;
        OutputDirectoryPath = outputDirectoryPath;
        TriggerCommand = triggerCommand;
        RemoveFileOnFileServerWhenCompleted = removeFileOnFileServerWhenCompleted;
        InitialLoadDirectories = initialLoadDirectories;
    }
}
