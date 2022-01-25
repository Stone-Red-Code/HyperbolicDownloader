using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HyperbolicDownloader.Networking;

internal class NetworkUtilities
{
    public static UnicastIPAddressInformation? GetUnicastIPAddressInformation(IPAddress address)
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (address.Equals(unicastIPAddressInformation.Address))
                    {
                        return unicastIPAddressInformation;
                    }
                }
            }
        }
        return null;
    }

    public static IPAddress? GetIP4Adress()
    {
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;

        return endPoint?.Address;
    }

    public static IPAddress GetBroadcastAddress(UnicastIPAddressInformation unicastAddress)
    {
        return GetBroadcastAddress(unicastAddress.Address, unicastAddress.IPv4Mask);
    }

    public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
        uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
        uint broadCastIpAddress = ipAddress | ~ipMaskV4;

        return new IPAddress(BitConverter.GetBytes(broadCastIpAddress));
    }
}