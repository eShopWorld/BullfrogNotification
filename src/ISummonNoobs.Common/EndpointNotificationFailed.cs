using Eshopworld.Core;

namespace ISummonNoobs.Common
{
    public class EndpointNotificationFailed : FabricInstanceBaseEvent
    {
        public string Url { get; set; }
        public string Reason { get; set; }
    }
}
