using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Newtonsoft.Json.Linq;

namespace ISummonNoobs.Interfaces
{
    public interface IISummonNoobsBackendService : IService //TODO: naming review
    {
        Task IngestMessage(string type, JObject message);
    }
}

