using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloaderApi
{
    internal class MessageRecivedEventArgs<T> : EventArgs
    {
        private readonly NetworkStream networkStream;

        public MessageRecivedEventArgs(NetworkStream networkStream, IPAddress ipAddress, T data)
        {
            this.networkStream = networkStream;
            Data = data;
            IpAddress = ipAddress;
        }

        public T Data { get; set; }

        public IPAddress IpAddress { get; set; }

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
}