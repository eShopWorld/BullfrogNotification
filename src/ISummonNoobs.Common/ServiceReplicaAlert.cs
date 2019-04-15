using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace ISummonNoobs.Common
{
    public class ServiceReplicaAlert : TelemetryEvent
    {
        public string AnomalyDetected { get; set; }
        public string ServiceUrl { get; set; }
    }
}
