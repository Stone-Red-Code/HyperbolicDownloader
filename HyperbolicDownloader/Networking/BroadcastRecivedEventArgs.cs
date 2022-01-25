using System.Net;

namespace HyperbolicDownloader.Networking;

internal class BroadcastRecivedEventArgs : EventArgs
{
    public BroadcastRecivedEventArgs(IPEndPoint iPEndPoint, string message)
    {
        IPEndPoint = iPEndPoint;
        Message = message;
    }

    public IPEndPoint IPEndPoint { get; }
    public string Message { get; }
}