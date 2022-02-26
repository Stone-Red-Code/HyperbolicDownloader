namespace HyperbolicDownloaderApi.Managment;

public class NotificationMessageEventArgs : EventArgs
{
    public NotificationMessageType NotificationMessageType { get; }
    public string? Message { get; }

    public NotificationMessageEventArgs(NotificationMessageType notificationMessageType, string? message)
    {
        NotificationMessageType = notificationMessageType;
        Message = message;
    }
}

public enum NotificationMessageType
{
    Info,
    Success,
    Warning,
    Error
}