using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Networking;

internal class MessageRecivedEventArgs<T>(NetworkStream networkStream, IPAddress ipAddress, T data) : EventArgs
{
    public T Data { get; set; } = data;

    public IPAddress IpAddress { get; set; } = ipAddress;

    public async Task SendResponseAsync(object response)
    {
        byte[] bytesToSend = Encoding.ASCII.GetBytes(JsonSerializer.Serialize(response));
        await networkStream.WriteAsync(bytesToSend);
    }

    public void SendResponse(object response)
    {
        SendResponseAsync(response).GetAwaiter().GetResult();
    }
}