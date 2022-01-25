namespace HyperbolicDowloader.Networking;

internal class NetworkSocket
{
    public NetworkSocket(string ipAddress, int port)
    {
        IPAddress = ipAddress;
        Port = port;
    }

    public string IPAddress { get; set; }
    public int Port { get; set; }
}