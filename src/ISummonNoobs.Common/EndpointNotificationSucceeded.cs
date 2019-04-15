using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace ISummonNoobs.Common
{
    public class EndpointNotificationSucceeded : FabricInstanceBaseEvent
    {
        public string Url { get; set; }
    }
}
