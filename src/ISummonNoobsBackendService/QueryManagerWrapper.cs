using System;
using System.Fabric;
using System.Fabric.Query;
using System.Threading.Tasks;

namespace ISummonNoobsBackendService
{
    public class QueryManagerWrapper
    {
        internal FabricClient.QueryClient queryClient { get; set; }

        public QueryManagerWrapper(FabricClient fc)
        {
            queryClient = fc.QueryManager;
        }

        public QueryManagerWrapper()
        {
            
        }
        public virtual async Task<ApplicationList> GetApplicationListAsync()
        {
            return await queryClient.GetApplicationListAsync();
        }

        public virtual async Task<ServiceList> GetServiceListAsync(Uri appName)
        {
            return await queryClient.GetServiceListAsync(appName);
        }

        public virtual async Task<ServicePartitionList> GetPartitionListAsync(Uri serviceName)
        {
            return await queryClient.GetPartitionListAsync(serviceName);
        }

        public virtual async Task<ServiceReplicaList> GetReplicaListAsync(Guid partitionId)
        {
            return await queryClient.GetReplicaListAsync(partitionId);
        }
    }
}
