using System;
using System.Fabric;
using System.Fabric.Query;
using System.Threading.Tasks;

namespace BullfrogNotificationBackendService
{
    public class QueryManagerWrapper
    {
        internal FabricClient.QueryClient QueryClient { get; set; }

        public QueryManagerWrapper(FabricClient fc)
        {
            QueryClient = fc.QueryManager;
        }

        public QueryManagerWrapper()
        {
            
        }
        public virtual async Task<ApplicationList> GetApplicationListAsync()
        {
            return await QueryClient.GetApplicationListAsync();
        }

        public virtual async Task<ServiceList> GetServiceListAsync(Uri appName)
        {
            return await QueryClient.GetServiceListAsync(appName);
        }

        public virtual async Task<ServicePartitionList> GetPartitionListAsync(Uri serviceName)
        {
            return await QueryClient.GetPartitionListAsync(serviceName);
        }

        public virtual async Task<ServiceReplicaList> GetReplicaListAsync(Guid partitionId)
        {
            return await QueryClient.GetReplicaListAsync(partitionId);
        }
    }
}
