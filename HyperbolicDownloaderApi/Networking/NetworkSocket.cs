namespace HyperbolicDownloaderApi.Networking;

public class NetworkSocket(string ipAddress, int port, DateTime lastActive)
{
    public string IPAddress { get; set; } = ipAddress;
    public int Port { get; set; } = port;
    public DateTime LastActive { get; set; } = lastActive;

    public long DownloadSpeed { get; set; }

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