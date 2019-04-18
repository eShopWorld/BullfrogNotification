using System;
using Eshopworld.Core;

namespace TestNotificationReceiver
{
    public class NotificationTestEvent : TelemetryEvent
    {
        public string Guid { get; set; }

        public NotificationTestEvent(string notificationGuid)
        {
            Guid = notificationGuid;
        }
    }
}
