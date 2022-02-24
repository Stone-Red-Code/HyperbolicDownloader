using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

namespace HyperbolicDownloaderApi.Commands;

public class ClientCommands
{
    public void ShowInfo(string _)
    {
        if (ApiManager.PublicIpAddress is not null)
        {
            ApiManager.SendMessageNewLine($"The public IP address is: {ApiManager.PublicIpAddress}", NotificationMessageType.Success);
            ApiManager.SendMessageNewLine($"The public port is: {ApiConfiguration.PublicPort}", NotificationMessageType.Success);
            ApiManager.SendMessageNewLine(string.Empty, NotificationMessageType.Raw);
        }

        ApiManager.SendMessageNewLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()}", NotificationMessageType.Success);
        ApiManager.SendMessageNewLine($"The private port is: {ApiConfiguration.PrivatePort}", NotificationMessageType.Success);
    }
}