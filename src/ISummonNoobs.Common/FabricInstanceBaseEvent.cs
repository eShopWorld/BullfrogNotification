using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace ISummonNoobs.Common
{
    public abstract class FabricInstanceBaseEvent : TelemetryEvent
    {
        public string ServiceUrl { get; set; }
        public string Node { get; set; }
        public long InstanceId { get; set; }
    }
}
