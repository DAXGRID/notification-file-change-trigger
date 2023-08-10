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

    // Path to the folder that the file server mainly looks for events from.
    // Example /integrations/ or just /
    [JsonPropertyName("fileServerDirectoryWatch")]
    public string FileServerDirectoryWatch { get; init; }

    [JsonConstructor]
    public Settings(
        NotificationServerSettings notificationServer,
        FileServerSettings fileServer,
        string fileServerDirectoryWatch)
    {
        NotificationServer = notificationServer;
        FileServer = fileServer;
        FileServerDirectoryWatch = fileServerDirectoryWatch;
    }
}
