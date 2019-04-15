using Autofac;
using Eshopworld.Web;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace BullfrogNotification.Common
{
    public class ServiceFabricModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register<IServiceProxyFactory>(ctx => new ServiceProxyFactory((c) => new FabricTransportActorRemotingClientFactory(
                null,
                c,
                serializationProvider: new ServiceRemotingJsonSerializationProvider())));
        }
    }
}
