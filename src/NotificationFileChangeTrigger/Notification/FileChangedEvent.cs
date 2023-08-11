using System.Text.Json.Serialization;

namespace NotificationFileChangeTrigger.Notification;

public sealed record FileChangedEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; }

    [JsonPropertyName("eventTimeStamp")]
    public DateTime EventTimeStamp { get; init; }

    [JsonPropertyName("fullPath")]
    public string FullPath { get; init; }

    public string FileName => Path.GetFileName(FullPath);

    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? "";

    public FileChangedEvent(string fullPath)
    {
        EventId = Guid.NewGuid();
        EventTimeStamp = DateTime.UtcNow;
        EventType = nameof(FileChangedEvent);
        FullPath = fullPath;
    }

    [JsonConstructor]
    public FileChangedEvent(
        Guid eventId,
        string eventType,
        DateTime eventTimeStamp,
        string fullPath)
    {
        EventId = eventId;
        EventType = eventType;
        EventTimeStamp = eventTimeStamp;
        FullPath = fullPath;
    }
}
