using Microsoft.AspNetCore.Mvc;
using PowerPlatform.Api.Models;
using PowerPlatform.Api.Services;

namespace PowerPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlowsController : ControllerBase
    {
        private readonly IPowerPlatformService _powerPlatformService;

        public FlowsController(IPowerPlatformService powerPlatformService)
        {
            _powerPlatformService = powerPlatformService;
        }

        [HttpGet("environment/{envId}")]
        public async Task<IActionResult> GetFlows(string envId)
        {
            var flows = await _powerPlatformService.GetFlowsAsync(envId);
            return Ok(flows);
        }

        [HttpGet("{envId}/{flowId}")]
        public async Task<IActionResult> GetFlowDefinition(string envId, string flowId)
        {
            var def = await _powerPlatformService.GetFlowDefinitionAsync(envId, flowId);
            return Ok(def);
        }
    }
}
