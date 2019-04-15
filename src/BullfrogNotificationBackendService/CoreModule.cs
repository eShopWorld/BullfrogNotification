using System;
using System.Fabric;
using System.Net.Http;
using Autofac;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace BullfrogNotificationBackendService
{
    internal class CoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => EswDevOpsSdk.BuildConfiguration());
            builder.RegisterType<ClusterNotifier>().SingleInstance();
            builder.Register(c => new FabricClient());
            builder.Register<IBigBrother>(c =>
                {
                    var config = c.Resolve<IConfigurationRoot>();
                    var bbInsKey = config["BBInstrumentationKey"];
                    return new BigBrother(bbInsKey, bbInsKey);
                })
                .SingleInstance();

            builder.Register(c => HttpPolicyExtensions.HandleTransientHttpError()
                    .WaitAndRetryAsync(new[]
                    {
                        //TODO: configure these timeouts and add jitter policy
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10)
                    }))
                .As<IAsyncPolicy<HttpResponseMessage>>()
                .SingleInstance();

            builder.Register(c => new PolicyHttpMessageHandler(c.Resolve<IAsyncPolicy<HttpResponseMessage>>()))
                .SingleInstance();

            builder.Register(c => new HttpClient(c.Resolve<PolicyHttpMessageHandler>()))
                .SingleInstance();
        }
    }
}
