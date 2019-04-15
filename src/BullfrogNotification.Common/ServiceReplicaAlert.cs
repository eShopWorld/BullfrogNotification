using Eshopworld.Core;

namespace BullfrogNotification.Common
{
    public class ServiceReplicaAlert : TelemetryEvent
    {
        public string AnomalyDetected { get; set; }
        public string ServiceUrl { get; set; }
    }
}
