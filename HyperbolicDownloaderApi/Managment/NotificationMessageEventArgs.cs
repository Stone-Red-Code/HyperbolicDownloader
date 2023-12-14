namespace HyperbolicDownloaderApi.Managment;

public class NotificationMessageEventArgs(NotificationMessageType notificationMessageType, string? message) : EventArgs
{
    public NotificationMessageType NotificationMessageType { get; } = notificationMessageType;
    public string? Message { get; } = message;
}

public enum NotificationMessageType
{
    Info,
    Log,
    Success,
    Warning,
    Error
}