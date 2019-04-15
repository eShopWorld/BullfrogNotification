using System.Runtime.Serialization;

namespace BullfrogNotification.Common
{
    [DataContract]
    public class InflightMessage
    {
        public InflightMessage(string payload, string type)
        {
            Payload = payload;
            Type = type;
        }

        [DataMember]
        public string Payload { get; set; }
        [DataMember]
        public string Type { get; set; }
    }
}
