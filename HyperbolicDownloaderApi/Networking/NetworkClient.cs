using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Networking;

internal class NetworkClient(FilesManager filesManager)
{
    private readonly Dictionary<string, (Type type, Delegate method)> events = [];
    private TcpListener? tcpListener;
    public bool IsListening { get; private set; } = false;

    public static async Task<T?> SendAsync<T>(IPAddress remoteIp, int remotePort, string eventName, object data)
    {
        ArgumentNullException.ThrowIfNull(remoteIp);

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
        ArgumentNullException.ThrowIfNull(remoteIp);

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
        if (IsListening)
        {
            throw new InvalidOperationException("Already listening!");
        }

        tcpListener = new TcpListener(IPAddress.Any, port);

        tcpListener.Start();
        IsListening = true;

        _ = new TaskFactory().StartNew(async () =>
        {
            while (IsListening)
            {
                try
                {
                    ApiManager.SendNotificationMessageNewLine("Listening for connections...", NotificationMessageType.Debug);

                    TcpClient client = tcpListener.AcceptTcpClient();

                    ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Connected", NotificationMessageType.Debug);

                    NetworkStream nwStream = client.GetStream();
                    byte[] buffer = new byte[client.ReceiveBufferSize];

                    ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Reading data", NotificationMessageType.Debug);

                    int bytesRead = await nwStream.ReadAsync(buffer.AsMemory(0, client.ReceiveBufferSize));

                    ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Data read", NotificationMessageType.Debug);

                    string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Data: {dataReceived}", NotificationMessageType.Debug);

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

                    if (dataContainer is not null && events.TryGetValue(dataContainer.EventName, out (Type type, Delegate method) value))
                    {
                        (Type type, Delegate method) = value;

                        Type eventArgsType = typeof(MessageRecivedEventArgs<>).MakeGenericType(type);

                        object? eventArgs = Activator.CreateInstance(
                            eventArgsType,
                            nwStream,
                            (client.Client.RemoteEndPoint as IPEndPoint)?.Address,
                            JsonSerializer.Deserialize(dataContainer.JsonData, type));

                        _ = (method?.DynamicInvoke(this, eventArgs));
                    }

                    client.Close();

                    ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Disconnected", NotificationMessageType.Debug);
                }
                catch (SocketException ex)
                {
                    ApiManager.SendNotificationMessageNewLine(ex.Message, NotificationMessageType.Log);
                }
            }
            tcpListener.Stop();
        }, TaskCreationOptions.LongRunning);
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

    private async Task Upload(TcpClient client, string hash)
    {
        try
        {
            ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Requesting file download [{hash}]", NotificationMessageType.Log);
            byte[] bytesToSend;
            hash = hash.Trim();
            NetworkStream nwStream = client.GetStream();
            client.SendBufferSize = 64000;
            if (filesManager.TryGet(hash, out PrivateHyperFileInfo? hyperFileInfo) && File.Exists(hyperFileInfo?.FilePath))
            {
                ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Accepting file download [{Path.GetFileName(hyperFileInfo.FilePath)}] [{hash}]", NotificationMessageType.Log);

                FileInfo fileInfo = new FileInfo(hyperFileInfo.FilePath);

                bytesToSend = Encoding.ASCII.GetBytes($"{fileInfo.Length}/{Path.GetFileName(hyperFileInfo.FilePath)}");

                Array.Resize(ref bytesToSend, 1000);

                await nwStream.WriteAsync(bytesToSend);

                ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Starting file download of file [{Path.GetFileName(hyperFileInfo.FilePath)}] [{hash}]", NotificationMessageType.Log);

                foreach (byte[]? chunk in FileCompressor.ReadChunks(hyperFileInfo.FilePath, 64000).Where(chunk => chunk is not null))
                {
                    await nwStream.WriteAsync(chunk);
                }

                ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Completed download of file [{Path.GetFileName(hyperFileInfo.FilePath)}] [{hash}]", NotificationMessageType.Log);
            }
            else
            {
                bytesToSend = Encoding.ASCII.GetBytes("File not found!");
                ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > File not found [{hash}]", NotificationMessageType.Log);
                await nwStream.WriteAsync(bytesToSend);
            }
        }
        catch (Exception ex)
        {
            ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Error downloading file {ex.Message} [{hash}]", NotificationMessageType.Log);
            Debug.WriteLine(ex);
        }
        finally
        {
            ApiManager.SendNotificationMessageNewLine($"{(client.Client.RemoteEndPoint as IPEndPoint)?.Address} > Closing connection", NotificationMessageType.Log);
            client.Close();
        }
    }
}