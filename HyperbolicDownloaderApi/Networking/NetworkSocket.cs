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

    public override bool Equals(object? obj)
    {
        if (obj is not NetworkSocket networkSocket)
        {
            return false;
        }

        return networkSocket.IPAddress == IPAddress && networkSocket.Port == Port;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IPAddress, Port);
    }
}