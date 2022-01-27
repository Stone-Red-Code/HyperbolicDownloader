namespace HyperbolicDownloader.Networking;

internal class NetworkSocket
{
    public string IPAddress { get; set; }
    public int Port { get; set; }

    public NetworkSocket(string ipAddress, int port)
    {
        IPAddress = ipAddress;
        Port = port;
    }
}