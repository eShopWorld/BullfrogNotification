using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Eshopworld.Telemetry;

namespace BullfrogNotificationBackendService
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
                builder.RegisterStatefulService<BullfrogNotificationBackendService>("BullfrogNotificationBackendServiceType");
                using (var container = builder.Build())
                {
                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
