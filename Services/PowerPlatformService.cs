using System.Net.Http.Headers;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using PowerPlatform.Api.Models;

namespace PowerPlatform.Api.Services
{
    public interface IPowerPlatformService
    {
        Task<IEnumerable<EnvironmentModel>> GetEnvironmentsAsync();
        Task<IEnumerable<FlowModel>> GetFlowsAsync(string environmentId);
        Task<FlowDefinitionModel> GetFlowDefinitionAsync(string environmentId, string flowId);
    }

    public class PowerPlatformService : IPowerPlatformService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PowerPlatformService> _logger;
        
        private readonly Dictionary<string, (string Token, DateTimeOffset ExpiresOn)> _tokenCache = new();
        private Dictionary<string, string>? _envNameCache = null;

        private async Task<string> GetEnvironmentNameAsync(string environmentId)
        {
            if (_envNameCache == null)
            {
                try
                {
                    var envs = await GetEnvironmentsAsync();
                    _envNameCache = envs.ToDictionary(e => e.EnvironmentId, e => e.EnvironmentName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to build environment name cache");
                    _envNameCache = new Dictionary<string, string>();
                }
            }

            if (_envNameCache.TryGetValue(environmentId, out var name))
            {
                return name;
            }
            return environmentId;
        }

        public PowerPlatformService(HttpClient httpClient, IConfiguration configuration, ILogger<PowerPlatformService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        private async Task<string> GetAccessTokenAsync(string scope)
        {
            if (_tokenCache.TryGetValue(scope, out var cached) && DateTimeOffset.UtcNow < cached.ExpiresOn.AddMinutes(-5))
            {
                return cached.Token;
            }

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
            
            _tokenCache[scope] = (result.AccessToken, result.ExpiresOn);

            return result.AccessToken;
        }

        public async Task<IEnumerable<EnvironmentModel>> GetEnvironmentsAsync()
        {
            var token = await GetAccessTokenAsync("https://api.bap.microsoft.com/.default");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("https://api.bap.microsoft.com/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments?api-version=2020-10-01");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var environments = new List<EnvironmentModel>();

            var valueArray = json["value"] as JArray;
            if (valueArray != null)
            {
                foreach (var item in valueArray)
                {
                    environments.Add(new EnvironmentModel
                    {
                        EnvironmentId = item["name"]?.ToString() ?? string.Empty,
                        EnvironmentName = item["properties"]?["displayName"]?.ToString() ?? string.Empty,
                        Location = item["location"]?.ToString() ?? string.Empty,
                        Type = item["properties"]?["environmentType"]?.ToString() ?? string.Empty
                    });
                }
            }
            return environments;
        }

        public async Task<IEnumerable<FlowModel>> GetFlowsAsync(string environmentId)
        {
            var token = await GetAccessTokenAsync("https://api.flow.microsoft.com/.default");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"https://api.flow.microsoft.com/providers/Microsoft.ProcessSimple/environments/{environmentId}/flows?api-version=2016-11-01");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var flows = new List<FlowModel>();

            var envName = await GetEnvironmentNameAsync(environmentId);

            var valueArray = json["value"] as JArray;
            if (valueArray != null)
            {
                foreach (var item in valueArray)
                {
                    var flowId = item["name"]?.ToString() ?? string.Empty;
                    var flowName = item["properties"]?["displayName"]?.ToString() ?? string.Empty;
                    var createdStr = item["properties"]?["createdTime"]?.ToString();
                    var modifiedStr = item["properties"]?["lastModifiedTime"]?.ToString();

                    System.DateTime? created = null;
                    if (System.DateTime.TryParse(createdStr, out var cVal)) created = cVal;

                    System.DateTime? modified = null;
                    if (System.DateTime.TryParse(modifiedStr, out var mVal)) modified = mVal;

                    var flowLink = $"https://make.powerautomate.com/environments/{environmentId}/flows/{flowId}/details";

                    flows.Add(new FlowModel
                    {
                        FlowId = flowId,
                        FlowName = flowName,
                        State = item["properties"]?["state"]?.ToString() ?? string.Empty,

                        ObjectId = flowId,
                        ObjectName = flowName,
                        ObjectCreated = created,
                        ObjectModified = modified,
                        ObjectLink = flowLink,
                        ObjectType = "Flow",
                        EnvironmentName = envName
                    });
                }
            }
            return flows;
        }

        public async Task<FlowDefinitionModel> GetFlowDefinitionAsync(string environmentId, string flowId)
        {
            var token = await GetAccessTokenAsync("https://api.flow.microsoft.com/.default");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"https://api.flow.microsoft.com/providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}?api-version=2016-11-01");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            
            return new FlowDefinitionModel
            {
                Properties = new FlowDefinitionProperties
                {
                    Definition = json["properties"]?["definition"]?.ToObject<object>()
                }
            };
        }
    }
}
