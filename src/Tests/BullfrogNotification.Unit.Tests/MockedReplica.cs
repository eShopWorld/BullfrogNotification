using System;
using System.Fabric.Health;
using System.Fabric.Query;

public class MockedReplica : Replica
{
    public MockedReplica(ServiceKind sc, long id, ServiceReplicaStatus ss, HealthState hs, string address, string nodeName, TimeSpan lastInBuildDuration):base(sc, id,ss, hs, address,nodeName, lastInBuildDuration)
    {
        
    }
}

