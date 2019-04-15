using System;
using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace ISummonNoobsBackendService
{
    [ExcludeFromCodeCoverage]
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main()
        {
            try
            {
                var builder = new ContainerBuilder();                
                builder.RegisterModule<CoreModule>();
                builder.RegisterServiceFabricSupport();
                builder.RegisterStatefulService<ISummonNoobsBackendService>("ISummonNoobsBackendServiceType");
                using (var container = builder.Build())
                {
                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
