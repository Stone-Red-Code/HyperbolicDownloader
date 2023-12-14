namespace HyperbolicDownloaderApi.Networking;

internal class DataContainer(string eventName, string jsonData)
{
    public string EventName { get; set; } = eventName;
    public string JsonData { get; set; } = jsonData;
}