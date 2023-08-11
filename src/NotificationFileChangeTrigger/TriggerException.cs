namespace NotificationFileChangeTrigger;

public class TriggerException : Exception
{
    public TriggerException() { }
    public TriggerException(string message) : base(message) { }
    public TriggerException(
        string message,
        Exception innerException) : base(message, innerException) { }
}
