using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Eshopworld.Telemetry;
using Eshopworld.Web;
using ISummonNoobs;
using ISummonNoobs.Common;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace ISummonNoobsAPIService
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
                    builder.RegisterStatelessService<ISummonNoobs.ISummonNoobsAPIService>("ISummonNoobsAPIServiceType");
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
