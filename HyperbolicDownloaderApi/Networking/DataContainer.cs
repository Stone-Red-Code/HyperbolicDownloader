namespace HyperbolicDownloaderApi
{
    internal class DataContainer
    {
        public string EventName { get; set; }
        public string JsonData { get; set; }

        public DataContainer(string eventName, string jsonData)
        {
            JsonData = jsonData;
            EventName = eventName;
        }
    }
}