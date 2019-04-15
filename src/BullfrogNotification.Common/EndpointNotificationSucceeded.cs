namespace BullfrogNotification.Common
{
    public class EndpointNotificationSucceeded : FabricInstanceBaseEvent
    {
        public string Url { get; set; }
    }
}
