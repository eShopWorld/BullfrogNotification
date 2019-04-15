using System;
using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Eshopworld.Core;
using ISummonNoobs.Common;
using Newtonsoft.Json.Linq;
using static System.String;

namespace ISummonNoobsBackendService
{
    /// <summary>
    ///
    /// some assumptions
    ///  - singleton partition for APIs
    ///  - APIs are stateless only
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ClusterNotifier
    {
        internal  HttpClient HttpClient;
        internal IBigBrother _bb;
        internal QueryManagerWrapper QueryManager;
        private static string MessageIngestUrlFormat = "api/v1/messageingest/{0}";

        public ClusterNotifier(FabricClient fabricClient, HttpClient httpClient, IBigBrother bb)
        {
            HttpClient = httpClient;
            _bb = bb;
            QueryManager = new QueryManagerWrapper(fabricClient);
        }

        internal ClusterNotifier()
        {
        }

        public virtual async Task DistributeToCluster(InflightMessage message, CancellationToken cancelToken, string itselfAppName)
        {
            ApplicationList apps;
            do
            {
                cancelToken.ThrowIfCancellationRequested();
                //distribute to apps
                apps = await QueryManager.GetApplicationListAsync();

                await Task.WhenAll(apps.Where(a=>!a.ApplicationName.AbsoluteUri.Equals(itselfAppName, StringComparison.OrdinalIgnoreCase)) //do not sent to itself //TODO: debug
                    .Select(a => DistributeToApp(a, message, cancelToken)));

            } while (!IsNullOrWhiteSpace(apps.ContinuationToken));
        }

        private async Task DistributeToApp(Application application, InflightMessage message, CancellationToken cancelToken)
        {
            ServiceList serviceList;
            
            do
            {
                cancelToken.ThrowIfCancellationRequested();

                //now distribute to services
                serviceList = await QueryManager.GetServiceListAsync(application.ApplicationName);
                await Task.WhenAll(serviceList.Where(s=>s.ServiceKind== ServiceKind.Stateless)
                    .Select(s => DistributeToService(s, message, cancelToken)));

            } while (!IsNullOrWhiteSpace(serviceList.ContinuationToken));
        }

        private async Task DistributeToService(Service service, InflightMessage message, CancellationToken cancelToken)
        {
            //here we have to identify APIs - ultimately by name but naming is not consistent at the moment
           
            //single partition only
            var parts = await QueryManager.GetPartitionListAsync(service.ServiceName);
            if (parts.Count == 0 && IsNullOrWhiteSpace(parts.ContinuationToken))
            {
                return;
            }

            if (parts.Count > 1 || !IsNullOrWhiteSpace(parts.ContinuationToken)) //there is more than 1 partition
            {
                return;
            }

            var partition = parts.First();

            cancelToken.ThrowIfCancellationRequested();

            var replicas = await QueryManager.GetReplicaListAsync(partition.PartitionInformation.Id); //stateless service - so no secondary replicas

            if (replicas==null || replicas.Count == 0 || !replicas.All(r =>
                r.HealthState == HealthState.Ok ||
                r.HealthState == HealthState.Warning))
            {
                //all considered unhealthy, do not try to ping them even
                
                _bb?.Publish(GetServiceReplicaAlert(service, "No healthy replica found"));
                return;
            }

            await Task.WhenAll(replicas.Select(r => DistributeToReplica(service, r, message, cancelToken)));
        }

        private async Task DistributeToReplica(Service service, Replica replica, InflightMessage message, CancellationToken cancelToken)
        {
            //find http endpoints
            var endpointObject = JObject.Parse(replica.ReplicaAddress);
            await Task.WhenAll(endpointObject["Endpoints"].ToList()
                .Where(e => e.First().ToString().StartsWith("http", StringComparison.OrdinalIgnoreCase)) //TODO: do http(s) regexp, I guess
                .Select(e => DistributeToEndpoint(service, replica, e.First().ToString(), message, cancelToken))); //Select vs First - replica could have multiple endpoints in theory
        }

        private async Task DistributeToEndpoint(Service service, Replica replica, string endpoint, InflightMessage message, CancellationToken cancelToken)
        {
            //TODO: security

            var failureDetected = false;
            var failureReason = Empty;
            try
            {
                UriBuilder uriBuilder = new UriBuilder(new Uri(endpoint));
                uriBuilder.Path = Format(CultureInfo.InvariantCulture, MessageIngestUrlFormat,
                    HttpUtility.UrlEncode(message.Type));

                var resp = await HttpClient.PostAsync(uriBuilder.Uri,
                    new StringContent(message.Payload, Encoding.UTF8, "application/json"),
                    cancelToken);

                if (!resp.IsSuccessStatusCode && resp.StatusCode!=HttpStatusCode.NotFound)
                {
                    failureDetected = true;
                    failureReason = $"Status Code returned - {resp.StatusCode}";
                }
            }
            catch (Exception e)
            {
                failureDetected = true;
                failureReason = e.Message;
            }
            finally
            {
                if (failureDetected)
                {
                    _bb?.Publish(GetEndpointFailureMessage(service, replica, endpoint, failureReason));
                }
                else
                {
                    _bb?.Publish(GetEndpointSuccessMessage(service, replica, endpoint));
                }
            }       
        }

        private static EndpointNotificationFailed GetEndpointFailureMessage(Service s, Replica r, string endpoint, string failureReason)
        {
            return new EndpointNotificationFailed()
            {
                ServiceUrl = s.ServiceName.ToString(),
                InstanceId = r.Id,
                Node = r.NodeName,
                Url = endpoint,
                Reason = failureReason
            };
        }

        private static EndpointNotificationSucceeded GetEndpointSuccessMessage(Service s, Replica r, string endpoint)
        {
            return new EndpointNotificationSucceeded()
            {
                ServiceUrl = s.ServiceName.ToString(),
                InstanceId = r.Id,
                Node = r.NodeName,
                Url = endpoint
            };
        }

        private static ServiceReplicaAlert GetServiceReplicaAlert(Service s, string reason)
        {
            return new ServiceReplicaAlert
                {AnomalyDetected = reason, ServiceUrl = s.ServiceName.ToString()};
        }
    }
}
