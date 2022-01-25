using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDowloader
{
    internal class NetworkClient
    {
        public bool IsListening { get; private set; } = false;

        private TcpListener? tcpListener;
        private readonly Dictionary<string, (Type type, Delegate method)> events = new();

        public NetworkClient()
        {
        }

        public static async Task<T?> SendAsync<T>(IPAddress remoteIp, int remotePort, string eventName, object data)
        {
            if (remoteIp is null)
            {
                throw new ArgumentNullException(nameof(remoteIp));
            }

            TcpClient client = new TcpClient();

            await client.ConnectAsync(remoteIp, remotePort);

            NetworkStream nwStream = client.GetStream();

            string stringData = JsonSerializer.Serialize(new DataContainer(eventName, JsonSerializer.Serialize(data)));
            byte[] bytesToSend = Encoding.ASCII.GetBytes(stringData);

            await nwStream.WriteAsync(bytesToSend);

            byte[] bytesToRead = new byte[client.ReceiveBufferSize];
            int bytesRead = await nwStream.ReadAsync(bytesToRead.AsMemory(0, client.ReceiveBufferSize));
            string response = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);

            client.Close();

            if (string.IsNullOrWhiteSpace(response))
            {
                return default;
            }
            else
            {
                return JsonSerializer.Deserialize<T>(response);
            }
        }

        public static async Task SendAsync(IPAddress remoteIp, int remotePort, string eventName, object data)
        {
            if (remoteIp is null)
            {
                throw new ArgumentNullException(nameof(remoteIp));
            }

            TcpClient client = new TcpClient();

            await client.ConnectAsync(remoteIp, remotePort);

            NetworkStream nwStream = client.GetStream();

            string stringData = JsonSerializer.Serialize(new DataContainer(eventName, JsonSerializer.Serialize(data)));
            byte[] bytesToSend = Encoding.ASCII.GetBytes(stringData);

            await nwStream.WriteAsync(bytesToSend);

            client.Close();
        }

        public static T? Send<T>(IPAddress remoteIp, int remotePort, string eventName, object data)
        {
            return SendAsync<T>(remoteIp, remotePort, eventName, data).GetAwaiter().GetResult();
        }

        public static void Send(IPAddress remoteIp, int remotePort, string eventName, object data)
        {
            SendAsync(remoteIp, remotePort, eventName, data).GetAwaiter().GetResult();
        }

        public void StartListening(int port)
        {
            if (IsListening == true)
            {
                throw new InvalidOperationException("Already listening!");
            }

            tcpListener = new TcpListener(IPAddress.Any, port);

            tcpListener.Start();
            IsListening = true;

            Task.Run(() =>
            {
                while (IsListening)
                {
                    try
                    {
                        using TcpClient client = tcpListener.AcceptTcpClient();
                        NetworkStream nwStream = client.GetStream();
                        byte[] buffer = new byte[client.ReceiveBufferSize];

                        int bytesRead = nwStream.Read(buffer, 0, client.ReceiveBufferSize);

                        string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (string.IsNullOrWhiteSpace(dataReceived))
                        {
                            continue;
                        }

                        DataContainer? dataContainer = JsonSerializer.Deserialize<DataContainer>(dataReceived);

                        if (dataContainer is not null && events.ContainsKey(dataContainer.EventName))
                        {
                            (Type type, Delegate method) = events[dataContainer.EventName];

                            Type eventArgsType = typeof(MessageRecivedEventArgs<>).MakeGenericType(type);

                            object? eventArgs = Activator.CreateInstance(
                                eventArgsType,
                                nwStream,
                                (client.Client.RemoteEndPoint as IPEndPoint)?.Address,
                                JsonSerializer.Deserialize(dataContainer.JsonData, type));

                            method?.DynamicInvoke(this, eventArgs);
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.Interrupted)
                        {
                            throw ex;
                        }
                    }
                }
                tcpListener.Stop();
            });
        }

        public void StopListening()
        {
            tcpListener?.Stop();
            IsListening = false;
        }

        public void ListenTo<T>(string eventName, EventHandler<MessageRecivedEventArgs<T>> eventHandler)
        {
            events.Add(eventName, (typeof(T), eventHandler));
        }
    }
}