using System.Net.Http.Headers;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using PowerPlatform.Api.Models;

namespace PowerPlatform.Api.Services
{
    /// <summary>
    /// Service responsible for discovering Key Vaults across all Power Platform environments
    /// and retrieving their secrets.
    /// </summary>
    public interface IKeyVaultService
    {
        /// <summary>
        /// Fetches secrets from Key Vaults associated with all Power Platform environments.
        /// Discovers environments via the BAP admin API, resolves linked Key Vault URIs,
        /// and returns a flattened list of secrets with environment context.
        /// </summary>
        Task<IEnumerable<KeyVaultSecretModel>> GetAllSecretsAsync();
    }

    public class KeyVaultService : IKeyVaultService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeyVaultService> _logger;

        public KeyVaultService(HttpClient httpClient, IConfiguration configuration, ILogger<KeyVaultService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Acquires an access token for the specified resource scope using MSAL Client Credentials flow.
        /// </summary>
        private async Task<string> GetAccessTokenAsync(string scope)
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];
            var authority = $"https://login.microsoftonline.com/{tenantId}";

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authority))
                .Build();

            string[] scopes = new string[] { scope };
            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }

        public async Task<IEnumerable<KeyVaultSecretModel>> GetAllSecretsAsync()
        {
            var allSecrets = new List<KeyVaultSecretModel>();

            // Step 1: Discover all environments from Power Platform admin API
            var environments = await GetEnvironmentsInternalAsync();

            // Step 2: For each environment, discover linked Key Vault URIs and fetch secrets
            var tasks = environments.Select(env => FetchSecretsForEnvironmentAsync(env, allSecrets));
            await Task.WhenAll(tasks);

            return allSecrets;
        }

        /// <summary>
        /// Fetches the list of Power Platform environments from the BAP admin API.
        /// </summary>
        private async Task<List<(string Id, string Name)>> GetEnvironmentsInternalAsync()
        {
            var environments = new List<(string Id, string Name)>();

            try
            {
                var token = await GetAccessTokenAsync("https://api.bap.microsoft.com/.default");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync(
                    "https://api.bap.microsoft.com/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments?api-version=2020-10-01");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var valueArray = json["value"] as JArray;

                if (valueArray != null)
                {
                    foreach (var item in valueArray)
                    {
                        var envId = item["name"]?.ToString() ?? string.Empty;
                        var envName = item["properties"]?["displayName"]?.ToString() ?? string.Empty;

                        // Look for linked Key Vault URIs in environment properties
                        environments.Add((envId, envName));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch environments for Key Vault discovery");
            }

            return environments;
        }

        /// <summary>
        /// Fetches Key Vault secrets for a specific environment.
        /// Attempts to resolve Key Vault URIs from environment configuration,
        /// then reads all secrets from those vaults.
        /// </summary>
        private async Task FetchSecretsForEnvironmentAsync(
            (string Id, string Name) env, List<KeyVaultSecretModel> allSecrets)
        {
            try
            {
                // Resolve Key Vault URIs for this environment.
                // Convention: Look for Key Vault URIs in appsettings under KeyVaults:{envId}
                // or use a discovery pattern (e.g., kv-{envId}.vault.azure.net)
                var vaultUris = GetVaultUrisForEnvironment(env.Id);
                var tenantId = _configuration["AzureAd:TenantId"];
                var clientId = _configuration["AzureAd:ClientId"];
                var clientSecret = _configuration["AzureAd:ClientSecret"];

                Azure.Core.TokenCredential credential = !string.IsNullOrEmpty(clientSecret) && clientSecret != "YOUR_CLIENT_SECRET"
                    ? new ClientSecretCredential(tenantId, clientId, clientSecret)
                    : new DefaultAzureCredential();

                foreach (var vaultUri in vaultUris)
                {
                    try
                    {
                        var client = new SecretClient(new Uri(vaultUri), credential);
                        var vaultName = new Uri(vaultUri).Host.Split('.')[0];

                        await foreach (var secretProperties in client.GetPropertiesOfSecretsAsync())
                        {
                            try
                            {
                                var secret = await client.GetSecretAsync(secretProperties.Name);
                                lock (allSecrets)
                                {
                                    allSecrets.Add(new KeyVaultSecretModel
                                    {
                                        SecretName = secret.Value.Name,
                                        SecretValue = secret.Value.Value ?? string.Empty,
                                        VaultName = vaultName,
                                        EnvironmentName = env.Name,
                                        EnvironmentId = env.Id,
                                        ContentType = secret.Value.Properties.ContentType ?? string.Empty,
                                        Enabled = secret.Value.Properties.Enabled ?? true,
                                        ExpiresOn = secret.Value.Properties.ExpiresOn
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to read secret {SecretName} from vault {VaultUri}",
                                    secretProperties.Name, vaultUri);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to access vault {VaultUri} for environment {EnvName}",
                            vaultUri, env.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Key Vault secrets for environment {EnvName}", env.Name);
            }
        }

        /// <summary>
        /// Resolves Key Vault URIs for a given environment ID.
        /// Checks appsettings configuration first, then falls back to a naming convention.
        /// </summary>
        private List<string> GetVaultUrisForEnvironment(string environmentId)
        {
            var uris = new List<string>();

            // Check if explicit vault URIs are configured for this environment
            var configuredVaults = _configuration.GetSection($"KeyVaults:{environmentId}").Get<string[]>();
            if (configuredVaults != null && configuredVaults.Length > 0)
            {
                uris.AddRange(configuredVaults);
            }

            // Check global vault list
            var globalVaults = _configuration.GetSection("KeyVaults:Global").Get<string[]>();
            if (globalVaults != null && globalVaults.Length > 0)
            {
                uris.AddRange(globalVaults);
            }

            return uris;
        }
    }
}
