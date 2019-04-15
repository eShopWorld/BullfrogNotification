using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using BullfrogNotification.Common;
using Eshopworld.Telemetry;
using Eshopworld.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace BullfrogNotificationApiService
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
                if (EnvironmentHelper.IsInFabric)
                {
                    var builder = new ContainerBuilder();
                    builder.RegisterModule<ServiceFabricModule>();
                    builder.RegisterServiceFabricSupport();
                    builder.RegisterStatelessService<BullfrogNotificationApiService>("BullfrogNotificationApiServiceType");
                    using (var container = builder.Build())
                    {
                        await Task.Delay(Timeout.Infinite);
                    }
                }
                else
                {
                    var host = WebHost.CreateDefaultBuilder()
                        .UseStartup<Startup>()
                        .Build();

                    host.Run();
                }
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
                throw;
            }
        }
    }
}
