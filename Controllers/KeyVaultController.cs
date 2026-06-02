using Microsoft.AspNetCore.Mvc;
using PowerPlatform.Api.Services;

namespace PowerPlatform.Api.Controllers
{
    /// <summary>
    /// Controller for retrieving Azure Key Vault secrets across all Power Platform environments.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class KeyVaultController : ControllerBase
    {
        private readonly IKeyVaultService _keyVaultService;
        private readonly ILogger<KeyVaultController> _logger;

        public KeyVaultController(IKeyVaultService keyVaultService, ILogger<KeyVaultController> logger)
        {
            _keyVaultService = keyVaultService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all secrets from Azure Key Vaults across all Power Platform environments.
        /// </summary>
        /// <returns>A list of Key Vault secrets with environment context.</returns>
        [HttpGet("secrets")]
        public async Task<IActionResult> GetAllSecrets()
        {
            try
            {
                var secrets = await _keyVaultService.GetAllSecretsAsync();
                return Ok(secrets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Key Vault secrets");
                return StatusCode(500, new { error = "Failed to retrieve Key Vault secrets", details = ex.Message });
            }
        }
    }
}
