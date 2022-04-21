using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

namespace HyperbolicDownloaderApi.Commands;

public class ClientCommands
{
    public void ShowInfo(string _)
    {
        if (ApiManager.PublicIpAddress is not null)
        {
            ApiManager.SendNotificationMessageNewLine($"The public IP address is: {ApiManager.PublicIpAddress}", NotificationMessageType.Info);
            ApiManager.SendNotificationMessageNewLine($"The public port is: {ApiConfiguration.PublicPort}", NotificationMessageType.Info);
            ApiManager.SendNotificationMessageNewLine(string.Empty, NotificationMessageType.Info);
        }

        ApiManager.SendNotificationMessageNewLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()}", NotificationMessageType.Info);
        ApiManager.SendNotificationMessageNewLine($"The private port is: {ApiConfiguration.PrivatePort}", NotificationMessageType.Info);
    }
}