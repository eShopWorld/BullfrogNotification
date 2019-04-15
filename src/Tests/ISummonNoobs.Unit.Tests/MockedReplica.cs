using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Text;

namespace ISummonNoobs.Unit.Tests
{
    //TODO: consider switching over to internal fabric sdk implementations
    public class MockedReplica : Replica
    {
        public MockedReplica(ServiceKind sc, long id, ServiceReplicaStatus ss, HealthState hs, string address, string nodeName, TimeSpan lastInBuildDuration):base(sc, id,ss, hs, address,nodeName, lastInBuildDuration)
        {
            
        }
    }
}
