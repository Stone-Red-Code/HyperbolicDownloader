using System.Net;

namespace HyperbolicDownloaderApi.Networking;

internal class BroadcastRecivedEventArgs(IPEndPoint ipEndPoint, string message) : EventArgs
{
    public IPEndPoint IPEndPoint { get; } = ipEndPoint;
    public string Message { get; } = message;
}