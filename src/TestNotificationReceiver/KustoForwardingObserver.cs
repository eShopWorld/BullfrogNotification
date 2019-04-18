using Eshopworld.Core;

namespace TestNotificationReceiver
{
    public class KustoForwardingObserver
    {
        private readonly IBigBrother _bb;

        public KustoForwardingObserver(IBigBrother bb)
        {
            _bb = bb;
        }

        public void ReceiveNotification(KeyVaultChangedNotification kvNotification)
        {
            _bb.Publish(new NotificationTestEvent(kvNotification.KeyVaultName));
        }
    }
}
