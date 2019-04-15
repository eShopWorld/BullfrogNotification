using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using BullfrogNotification.Common;
using BullfrogNotificationBackendService;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Http;
using Moq;
using RichardSzalay.MockHttp;
using ServiceFabric.Mocks;
using Xunit;

[Collection(nameof(AutofacTestFixtureCollection))]
// ReSharper disable once CheckNamespace
public class ClusterNotifierTests
{
private readonly AutofacTestFixture _fixture;

public ClusterNotifierTests(AutofacTestFixture fixture)
{
    _fixture = fixture;
}

[Fact, IsDev]
public async Task TestRetriesFlow_FailAllTheWay()
{
    var (httpClient, mockHandler) = GetHttpMocks((handler =>
        {
            handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        }));

    var resp = await httpClient.GetAsync("http://home");
    resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
}

[Fact, IsDev]
public async Task TestRetriesFlow_NonTransientIssue()
{
    MockedRequest nextReq = null;
    var (httpClient, mockHandler) = GetHttpMocks((handler =>
    {
        handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        nextReq = handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
    }));

    var resp = await httpClient.GetAsync("http://home");
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    mockHandler.GetMatchCount(nextReq).Should().Be(0);
}

[Fact, IsDev]
public async Task TestRetriesFlow_EventuallySucceed()
{
    var (httpClient, mockHandler) = GetHttpMocks((handler =>
    {
        handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        handler.Expect("http://home").Respond((req) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    }));

    var resp = await httpClient.GetAsync("http://home");
    resp.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact, IsLayer0]
public async Task BasicFlow_EndpointsDiscoveredAndPinged()
{
    var notifier = new ClusterNotifier();
    var queryManagerMock = new Mock<QueryManagerWrapper>();

    var appList = new ApplicationList();
    for (var x = 0; x < 3; x++)
    {
        //setup basic deployed evo-app in query manager
        AddAppMock(queryManagerMock, $"App{x}", appList);
    }

    queryManagerMock.Setup(i => i.GetApplicationListAsync()).ReturnsAsync(appList);

    var endpoints = new List<MockedRequest>();

    var (httpClient, mockHandler) = GetHttpMocks((handler =>
    {
        for (var x = 0; x < 3; x++)
        {
            endpoints.Add(handler.When($"http://App{x}:1234/notification/type%2bclass%2cassembly")
                .Respond((req) =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode
                            .OK)))); //set this as backend expectation            
        }
    }));

    notifier.HttpClient = httpClient;
    notifier.QueryManager = queryManagerMock.Object;

    var token= new CancellationToken();

    try
    {
        await notifier.DistributeToCluster(new InflightMessage("blah", "type+class,assembly"), token, "notifierApp");
    }
    catch (Exception e)
    {
        
    }
    
    foreach (var endpoint in endpoints)
    {
        mockHandler.GetMatchCount(endpoint).Should().Be(1);
    }
}

private static void AddAppMock(Mock<QueryManagerWrapper> queryManagerMock, string appName, ApplicationList appList)
{

    appList.Add(MockQueryApplicationFactory.CreateApplication(appName));

    queryManagerMock.Setup(i => i.GetServiceListAsync(It.Is<Uri>(uri => uri.AbsoluteUri == $"fabric:/{appName}")))
        .ReturnsAsync(new ServiceList()
        {
            CreateStatelessServiceInstance(new Uri($"fabric:/{appName}Service"), "apiServiceType", "1.0", HealthState.Ok,
                ServiceStatus.Active)
        });

  
    var partGuid = Guid.NewGuid();

    var partition = CreateServicePartition(partGuid);

    queryManagerMock
        .Setup(i => i.GetPartitionListAsync(It.Is<Uri>(uri => uri.AbsoluteUri == $"fabric:/{appName}Service")))
        .ReturnsAsync(new ServicePartitionList()
        {
            partition
        });

    queryManagerMock.Setup(i => i.GetReplicaListAsync(It.Is<Guid>(g=>g==partGuid))).ReturnsAsync(
        new ServiceReplicaList()
        {
            new MockedReplica(ServiceKind.Stateless, 1, ServiceReplicaStatus.Ready, HealthState.Ok,
                GetReplicaAddressJson($"http://{appName}:1234"), "nodeA", TimeSpan.MinValue)
        });
}

private static Partition CreateServicePartition(Guid partGuid)
{
    var partition = MockQueryPartitionFactory.CreateStatelessPartition(new SingletonPartitionInformation(), 1,
        HealthState.Ok, ServicePartitionStatus.Ready);
    var type = partition.PartitionInformation.GetType();

    type.GetProperty("Id").SetValue(partition.PartitionInformation, partGuid);
    return partition;
}

private static string GetReplicaAddressJson(string url)
{
    return $"{{\"Endpoints\" :{{ \"mylistener1\" : \"{url}\" }}}}";
}
private (HttpClient httpClient, MockHttpMessageHandler mockHandler) GetHttpMocks(Action<MockHttpMessageHandler> handlerSetUp)
{
    var handler  = new MockHttpMessageHandler();
    handlerSetUp.Invoke(handler);

    var pollyHandler = _fixture.Container.Resolve<PolicyHttpMessageHandler>();
    pollyHandler.InnerHandler = handler;

    return (new HttpClient(pollyHandler), handler);
}

private static StatelessService CreateStatelessServiceInstance(
    Uri serviceName,
    string serviceTypeName,
    string serviceManifestVersion,
    HealthState healthState,
    ServiceStatus serviceStatus,
    bool isServiceGroup = false)
{
    var item= (StatelessService)Activator.CreateInstance(typeof(StatelessService), BindingFlags.Instance | BindingFlags.NonPublic, (Binder)null, new object[]
    {
        serviceName,
        serviceTypeName,
        serviceManifestVersion,
        healthState,
        serviceStatus,
        isServiceGroup
    }, CultureInfo.CurrentCulture);

    return item;
}
}
