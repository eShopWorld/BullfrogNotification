using System.Runtime.Serialization;

namespace ISummonNoobs.Common
{
    [DataContract]
    public class InflightMessage
    {
        [DataMember]
        public string Payload { get; set; }
        [DataMember]
        public string Type { get; set; }
    }
}
