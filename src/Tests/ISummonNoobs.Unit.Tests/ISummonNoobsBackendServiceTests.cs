using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Tests.Core;
using FluentAssertions;
using ISummonNoobs.Common;
using ISummonNoobsBackendService;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using Newtonsoft.Json.Linq;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReliableCollections;
using ServiceFabric.Mocks.ReplicaSet;
using Xunit;

// ReSharper disable once CheckNamespace
// ReSharper disable once InconsistentNaming
public class ISummonNoobsBackendServiceTests
{
    public ISummonNoobsBackendService.ISummonNoobsBackendService CreateBackendService(StatefulServiceContext sc,
        IReliableStateManagerReplica2 stateManagerReplica2) =>
        new ISummonNoobsBackendService.ISummonNoobsBackendService(sc, stateManagerReplica2, null);

    public JObject JsonMessage => JObject.Parse("{\"prop\":\"blah\"}");

    [Fact, IsLayer0]
    public async Task IngestMessageTest()
    {
        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service = new ISummonNoobsBackendService.ISummonNoobsBackendService(context, stateManager, Mock.Of<ClusterNotifier>());
        
        await service.IngestMessage("blah", JsonMessage);
        var queue = await stateManager.TryGetAsync<IReliableConcurrentQueue<InflightMessage>>(ISummonNoobsBackendService.ISummonNoobsBackendService.QueueName);
        var message = (await queue.Value.TryDequeueAsync(new MockTransaction(stateManager, 1))).Value;
        message.Should().NotBeNull();
        message.Payload.Should().Be(JsonMessage.ToString());
    }

    [Fact, IsLayer0]
    public async Task MessageDequeued_NotifierCalled()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>())).Returns(Task.CompletedTask).Verifiable();

        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service = new ISummonNoobsBackendService.ISummonNoobsBackendService(context, stateManager, clusterNotifierMock.Object);

        await service.IngestMessage("blah", JsonMessage);
        var ctxToken = new CancellationToken();
        var T = service.InvokeRunAsync(ctxToken);
        T.Wait(TimeSpan.FromSeconds(2));

        var queue = await stateManager.TryGetAsync<IReliableConcurrentQueue<InflightMessage>>(ISummonNoobsBackendService.ISummonNoobsBackendService.QueueName);
        var message = (await queue.Value.TryDequeueAsync(new MockTransaction(stateManager, 1), ctxToken)).Value;
        message.Should().BeNull();
        clusterNotifierMock.VerifyAll();
    }

    [Fact, IsLayer0]
    public async Task PrimaryReplicaTest()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        var notifierSignal= new ManualResetEvent(false);
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>()))
            .Callback((() => notifierSignal.Set())).Returns(Task.CompletedTask);
           

        var replicaSet = new MockStatefulServiceReplicaSet<ISummonNoobsBackendService.ISummonNoobsBackendService>((sc,stateManagerReplica2) =>
        new ISummonNoobsBackendService.ISummonNoobsBackendService(sc, stateManagerReplica2, clusterNotifierMock.Object), CreateStateManagerReplica);

        await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);     
        await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 2);
        await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 3);

        await replicaSet.Primary.ServiceInstance.IngestMessage("blah", JsonMessage);
        notifierSignal.WaitOne(TimeSpan.FromSeconds(2));
    }

    private IReliableStateManagerReplica2 CreateStateManagerReplica(StatefulServiceContext ctx, TransactedConcurrentDictionary<Uri, IReliableState> states)
    {
        return new MockReliableStateManager(states);
    }
}
