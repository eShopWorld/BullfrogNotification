using System;
using System.Net;
using System.Threading.Tasks;
using BullfrogNotification.Common;
using BullfrogNotification.Interfaces;
using Eshopworld.Core;
using Eshopworld.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Newtonsoft.Json.Linq;

namespace ISummonNoobsAPIService.Controllers
{
    /// <summary>
    /// sample controller
    /// </summary>
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Authorize]
    public class MessageIngestController : Controller
    {
        private readonly IBigBrother _bigBrother;

        /// <summary>
        /// DI constructor
        /// </summary>
        /// <param name="bigBrother">big brother instance</param>
        public MessageIngestController(IBigBrother bigBrother)
        {
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// post
        /// </summary>
        /// <param name="payload">payload - JSON</param>
        /// <param name="type">message type</param>
        /// <returns>action result</returns>
        /// <response code="201">A new value was created</response>
        /// <response code="400">The request is malformed</response>
        /// <response code="401">Caller is Unauthorized</response>
        [HttpPost("{type}")]
        [ProducesResponseType((int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Post([FromBody]JObject payload, string type)
        {

            var proxyFactory = new ServiceProxyFactory((c) => new FabricTransportActorRemotingClientFactory(
                fabricTransportRemotingSettings: null,
                callbackMessageHandler: c,
                serializationProvider: new ServiceRemotingJsonSerializationProvider()));

            var service =
                proxyFactory.CreateServiceProxy<IBullfrogNotificationBackendService>(
                    new Uri("fabric:/ISummonNoobs/ISummonNoobsBackendService"), new ServicePartitionKey(1)); //TODO: review keying strategy

            await service.IngestMessage(type, payload);

            _bigBrother.Publish(new MessagedIngested { MessageType = type, Payload = payload.ToString()});

            return Ok();
        }

 
    }
}
