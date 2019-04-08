using System.Threading.Tasks;
using ISummonNoobs.Common;

namespace ISummonNoobsBackendService
{
    public class ClusterNotifier
    {
        
        public  async virtual Task DistributeToCluster(InflightMessage message)
        {
            //var fc = new FabricClient();
            //ApplicationList list = null;
            //do
            //{
            //    list = list == null
            //        ? await fc.QueryManager.GetApplicationListAsync(null)
            //        : await fc.QueryManager.GetApplicationListAsync(null, list.ContinuationToken);
            //    foreach (var app in list)
            //    {

            //    }
                
            //} while (!IsNullOrWhiteSpace(list.ContinuationToken));
            
        }
    }
}
