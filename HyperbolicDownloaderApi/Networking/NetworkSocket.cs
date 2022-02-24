namespace HyperbolicDownloaderApi.Networking;

public class NetworkSocket
{
    public string IPAddress { get; set; }
    public int Port { get; set; }
    public DateTime LastActive { get; set; }

    public NetworkSocket(string ipAddress, int port, DateTime lastActive)
    {
        IPAddress = ipAddress;
        Port = port;
        LastActive = lastActive;
    }
}