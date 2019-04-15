using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Newtonsoft.Json.Linq;

namespace BullfrogNotification.Interfaces
{
    public interface IBullfrogNotificationBackendService : IService
    {
        Task IngestMessage(string type, JObject message);
    }
}

