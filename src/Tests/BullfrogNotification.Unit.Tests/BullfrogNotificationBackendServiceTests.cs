using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using BullfrogNotification.Common;
using BullfrogNotificationBackendService;
using Eshopworld.Tests.Core;
using FluentAssertions;
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
public class BullfrogNotificationBackendServiceTests
{
    public BullfrogNotificationBackendService.BullfrogNotificationBackendService CreateBackendService(StatefulServiceContext sc,
        IReliableStateManagerReplica2 stateManagerReplica2) =>
        new BullfrogNotificationBackendService.BullfrogNotificationBackendService(sc, stateManagerReplica2, null);

    private static JObject JsonMessage => JObject.Parse("{\"prop\":\"blah\"}");

    [Fact, IsLayer0]
    public async Task IngestMessageTest()
    {
        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service = new BullfrogNotificationBackendService.BullfrogNotificationBackendService(context, stateManager, Mock.Of<ClusterNotifier>())
        {
            WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
        };

        await service.IngestMessage(JsonMessage.ToString(), JsonMessage);
        var queue = await stateManager.TryGetAsync<IReliableConcurrentQueue<InflightMessage>>(BullfrogNotificationBackendService.BullfrogNotificationBackendService.QueueName);
        var message = (await queue.Value.TryDequeueAsync(new MockTransaction(stateManager, 1))).Value;
        message.Should().NotBeNull();
        message.Payload.Should().Be(JsonMessage.ToString());
    }

    [Fact, IsLayer0]
    public async Task MessageDequeued_NotifierCalled()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>(), It.IsAny<CancellationToken>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service =
            new BullfrogNotificationBackendService.BullfrogNotificationBackendService(context, stateManager, clusterNotifierMock.Object)
            {
                WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
            };

        await service.IngestMessage(JsonMessage.ToString(), JsonMessage);
        var ctxToken = new CancellationToken();
        var T = service.InvokeRunAsync(ctxToken);
        T.Wait(TimeSpan.FromMilliseconds(30));

        var queue = await stateManager.TryGetAsync<IReliableConcurrentQueue<InflightMessage>>(BullfrogNotificationBackendService.BullfrogNotificationBackendService.QueueName);
        var message = (await queue.Value.TryDequeueAsync(new MockTransaction(stateManager, 1), ctxToken)).Value;
        message.Should().BeNull();
        clusterNotifierMock.VerifyAll();
    }

    [Fact, IsLayer0]
    public async Task CancellationTokenChecks()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>(), It.IsAny<CancellationToken>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service =
            new BullfrogNotificationBackendService.BullfrogNotificationBackendService(context, stateManager, clusterNotifierMock.Object)
            {
                WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
            };

        await service.IngestMessage(JsonMessage.ToString(), JsonMessage);
        var ctxSource = new CancellationTokenSource();
        
        var T = service.InvokeRunAsync(ctxSource.Token);
        T.Wait(TimeSpan.FromMilliseconds(30));
        ctxSource.Cancel();
        Thread.Sleep(TimeSpan.FromSeconds(2)); //TODO: maybe rewrite
        T.IsCanceled.Should().BeTrue();
    }

    [Fact, IsLayer0]
    public async Task ClusterNotificationThrows_MessageKeptInQueue()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>(), It.IsAny<CancellationToken>(), It.IsAny<string>())).Throws<Exception>().Verifiable();

        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service =
            new BullfrogNotificationBackendService.BullfrogNotificationBackendService(context, stateManager, clusterNotifierMock.Object)
            {
                WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
            };

        await service.IngestMessage(JsonMessage.ToString(), JsonMessage);
        var ctxSource = new CancellationTokenSource();

        var T = service.InvokeRunAsync(ctxSource.Token);
        T.Wait(TimeSpan.FromMilliseconds(30));
        var queue = await stateManager.TryGetAsync<IReliableConcurrentQueue<InflightMessage>>(BullfrogNotificationBackendService.BullfrogNotificationBackendService.QueueName);
        var message = (await queue.Value.TryDequeueAsync(new MockTransaction(stateManager, 1), ctxSource.Token)).Value;
        message.Should().NotBeNull();
    }

    [Fact, IsLayer0]
    public async Task ClusterNotificationCancels_ServiceCancels()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>(), It.IsAny<CancellationToken>(), It.IsAny<string>())).Throws<OperationCanceledException>().Verifiable();

        var context = MockStatefulServiceContextFactory.Default;
        var stateManager = new MockReliableStateManager();
        var service =
            new BullfrogNotificationBackendService.BullfrogNotificationBackendService(context, stateManager, clusterNotifierMock.Object)
            {
                WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
            };

        await service.IngestMessage(JsonMessage.ToString(), JsonMessage);
        var ctxSource = new CancellationTokenSource();

        var T = service.InvokeRunAsync(ctxSource.Token);
        Thread.Sleep(TimeSpan.FromMilliseconds(30)); 
        T.IsCanceled.Should().BeTrue();
    }

    [Fact, IsLayer0]
    public async Task PrimaryReplicaTest()
    {
        var clusterNotifierMock = new Mock<ClusterNotifier>();
        var notifierSignal= new ManualResetEvent(false);
        clusterNotifierMock.Setup(i => i.DistributeToCluster(It.IsAny<InflightMessage>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .Callback((() => notifierSignal.Set())).Returns(Task.CompletedTask);
           

        var replicaSet = new MockStatefulServiceReplicaSet<BullfrogNotificationBackendService.BullfrogNotificationBackendService>(
            (sc, stateManagerReplica2) => new BullfrogNotificationBackendService.BullfrogNotificationBackendService(sc, stateManagerReplica2,
                clusterNotifierMock.Object)
            {
                WaitTimeBetweenLoop = TimeSpan.FromMilliseconds(10)
            }, CreateStateManagerReplica);

        await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);     
        await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 2);
        await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 3);

        await replicaSet.Primary.ServiceInstance.IngestMessage("blah", JsonMessage);
        notifierSignal.WaitOne(TimeSpan.FromMilliseconds(30));
    }

    private IReliableStateManagerReplica2 CreateStateManagerReplica(StatefulServiceContext ctx, TransactedConcurrentDictionary<Uri, IReliableState> states)
    {
        return new MockReliableStateManager(states);
    }
}
