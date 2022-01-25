using Stone_Red_Utilities.ConsoleExtentions;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace HyperbolicDowloader.Networking;

internal class BroadcastClient
{
    public event EventHandler<BroadcastRecivedEventArgs>? OnBroadcastRecived;

    public bool IsListening { get; private set; } = false;

    private UdpClient? udpListener;

    public void Send(int port, string message)
    {
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPAddress? ip4Address = NetworkUtilities.GetIP4Adress();

        if (ip4Address is null)
        {
            ConsoleExt.WriteLine("Could not find suitable network adapter!", ConsoleColor.Red);
            return;
        }

        UnicastIPAddressInformation? addressInformation = NetworkUtilities.GetUnicastIPAddressInformation(ip4Address);

        if (addressInformation is null)
        {
            ConsoleExt.WriteLine("Could not find suitable network adapter!", ConsoleColor.Red);
            return;
        }

        IPAddress broadcast = NetworkUtilities.GetBroadcastAddress(addressInformation);

        byte[] sendbuf = Encoding.ASCII.GetBytes(message);
        IPEndPoint ep = new IPEndPoint(broadcast, port);

        s.SendTo(sendbuf, ep);
    }

    public void StartListening(int port)
    {
        if (IsListening == true)
        {
            throw new InvalidOperationException("Already listening!");
        }

        IsListening = true;

        udpListener = new UdpClient(port);
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, port);

        Task.Run(() =>
        {
            while (IsListening)
            {
                byte[] bytes = udpListener.Receive(ref groupEP);
                string message = Encoding.ASCII.GetString(bytes, 0, bytes.Length);

                OnBroadcastRecived?.Invoke(this, new BroadcastRecivedEventArgs(groupEP, message));
            }
        });
    }

    public void StopListening()
    {
        udpListener?.Close();
        IsListening = false;
    }
}