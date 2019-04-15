using Eshopworld.Core;

namespace BullfrogNotification.Common
{
    public abstract class FabricInstanceBaseEvent : TelemetryEvent
    {
        public string ServiceUrl { get; set; }
        public string Node { get; set; }
        public long InstanceId { get; set; }
    }
}
