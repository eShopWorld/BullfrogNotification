using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using BullfrogNotification.Common;
using BullfrogNotification.Interfaces;
using Eshopworld.Core;
using Eshopworld.Web;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json.Linq;

namespace BullfrogNotificationBackendService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public class BullfrogNotificationBackendService : StatefulService, IBullfrogNotificationBackendService
    {
        private readonly ClusterNotifier _clusterNotifier;
        private readonly IBigBrother _bigBrother;
        internal const string QueueName = "NotificationInflightQueue";

        private readonly object _inflightQueueInstanceLock = new object();
        private IReliableConcurrentQueue<InflightMessage> _inflightQueue;

        internal TimeSpan WaitTimeBetweenLoop { get; set; } = TimeSpan.FromSeconds(1);

        public BullfrogNotificationBackendService(StatefulServiceContext context, ClusterNotifier clusterNotifier, IBigBrother bigBrother)
            : base(context)
        {
            _clusterNotifier = clusterNotifier;
            _bigBrother = bigBrother;
        }

        public BullfrogNotificationBackendService(StatefulServiceContext context, IReliableStateManagerReplica stateManager, ClusterNotifier clusterNotifier, IBigBrother bigBrother)
            : base(context, stateManager)
        {
            _clusterNotifier = clusterNotifier;
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// register listeners
        ///
        /// we are using JSON serialization instead of standard remoting DataContract
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return  new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener((c) => new FabricTransportServiceRemotingListener(Context, this, null,
                    new ServiceRemotingJsonSerializationProvider()))
            };
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            InitInflightQueue();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var tx = StateManager.CreateTransaction())
                    {

                        var message = await _inflightQueue.TryDequeueAsync(tx, cancellationToken);
                        if (message.HasValue)
                        {

                            await _clusterNotifier.DistributeToCluster(message.Value, cancellationToken,
                                Context.CodePackageActivationContext.ApplicationName);
                        }

                        await tx.CommitAsync();
                    }
                }
                catch (OperationCanceledException) //cancellation requested
                {
                    throw;
                }
                catch (Exception e) //anything else
                {
                    _bigBrother.Publish(e.ToExceptionEvent());
                }

                await Task.Delay(WaitTimeBetweenLoop, cancellationToken);

            }
            // ReSharper disable once FunctionNeverReturns
        }

        /// <summary>
        /// enqueue incoming message
        /// </summary>
        /// <param name="type">type of message</param>
        /// <param name="message">message payload</param>
        /// <returns>async task</returns>
        public async Task IngestMessage(string type, JObject message)
        {
            InitInflightQueue();
            using (var tx = StateManager.CreateTransaction())
            {
                await _inflightQueue.EnqueueAsync(tx, new InflightMessage (message.ToString(), type ));

                await tx.CommitAsync();
            }
        }

        /// <summary>
        /// this service has two runtime aspects - <see cref="RunAsync"/> and <see cref="IBullfrogNotificationBackendService"/>        
        /// as soon as listener is open, messaging can start flowing in and the same goes for <see cref="RunAsync"/> kicking in
        ///
        /// the listeners are not open to secondary replicas and the same goes for <see cref="RunAsync"/> not being invoked on those so ultimately the queue can be shared
        /// within the partition/primary replica instance (and we will likely run one partition anyway)
        /// </summary>
        private void InitInflightQueue()
        {
            if (_inflightQueue != null) return;

            lock (_inflightQueueInstanceLock)
            {
                if (_inflightQueue == null)
                {
                    _inflightQueue =
                        StateManager.GetOrAddAsync<IReliableConcurrentQueue<InflightMessage>>(QueueName).GetAwaiter().GetResult();
                }
            }
        }
    }
}
