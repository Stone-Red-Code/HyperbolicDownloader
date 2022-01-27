using HyperbolicDownloader.FileProcessing;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloader.Networking
{
    internal class NetworkClient
    {
        public bool IsListening { get; private set; } = false;

        private TcpListener? tcpListener;
        private readonly FilesManager filesManager;
        private readonly Dictionary<string, (Type type, Delegate method)> events = new();

        public NetworkClient(FilesManager filesManager)
        {
            this.filesManager = filesManager;
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

            Task.Run(async () =>
            {
                while (IsListening)
                {
                    try
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        NetworkStream nwStream = client.GetStream();
                        byte[] buffer = new byte[client.ReceiveBufferSize];

                        int bytesRead = await nwStream.ReadAsync(buffer.AsMemory(0, client.ReceiveBufferSize));

                        string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (dataReceived.StartsWith("Download"))
                        {
                            _ = Upload(client, dataReceived[8..]);
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(dataReceived))
                        {
                            client.Close();
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

                        client.Close();
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

        private async Task Upload(TcpClient client, string hash)
        {
            byte[] bytesToSend;
            hash = hash.Trim();
            NetworkStream nwStream = client.GetStream();
            client.SendBufferSize = 64000;
            if (filesManager.TryGet(hash, out HyperFileInfo? hyperFileInfo) && File.Exists(hyperFileInfo?.FilePath))
            {
                if (hash != await FileValidator.CalculateHashAsync(hyperFileInfo.FilePath))
                {
                    bytesToSend = Encoding.ASCII.GetBytes("Hash does not match!");
                    await nwStream.WriteAsync(bytesToSend);
                    return;
                }

                //string compressedFilePath = $"{hash + DateTime.Now.Millisecond}.temp";

                //FileCompressor.CompressFile(hyperFileInfo.FilePath, compressedFilePath);

                FileInfo compressedFileInfo = new FileInfo(hyperFileInfo.FilePath);

                bytesToSend = Encoding.ASCII.GetBytes($"{compressedFileInfo.Length}/{Path.GetFileName(hyperFileInfo.FilePath)}");
                await nwStream.WriteAsync(bytesToSend);

                foreach (byte[]? chunk in FileCompressor.ReadChunks(hyperFileInfo.FilePath, 64000))
                {
                    if (chunk is not null)
                    {
                        await nwStream.WriteAsync(chunk);
                    }
                }

                // File.Delete(compressedFilePath);
            }
            else
            {
                bytesToSend = Encoding.ASCII.GetBytes("File not found!");
                await nwStream.WriteAsync(bytesToSend);
            }
            client.Close();
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