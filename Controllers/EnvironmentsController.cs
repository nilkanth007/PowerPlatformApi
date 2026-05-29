using Microsoft.AspNetCore.Mvc;
using PowerPlatform.Api.Models;
using PowerPlatform.Api.Services;

namespace PowerPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnvironmentsController : ControllerBase
    {
        private readonly IPowerPlatformService _powerPlatformService;

        public EnvironmentsController(IPowerPlatformService powerPlatformService)
        {
            _powerPlatformService = powerPlatformService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEnvironments()
        {
            var envs = await _powerPlatformService.GetEnvironmentsAsync();
            return Ok(envs);
        }
    }
}
