using Newtonsoft.Json;

namespace PowerPlatform.Api.Models
{
    /// <summary>
    /// Represents a secret retrieved from an Azure Key Vault tied to a Power Platform environment.
    /// </summary>
    public class KeyVaultSecretModel
    {
        [JsonProperty("secretName")]
        public string SecretName { get; set; } = string.Empty;

        [JsonProperty("secretValue")]
        public string SecretValue { get; set; } = string.Empty;

        [JsonProperty("vaultName")]
        public string VaultName { get; set; } = string.Empty;

        [JsonProperty("environmentName")]
        public string EnvironmentName { get; set; } = string.Empty;

        [JsonProperty("environmentId")]
        public string EnvironmentId { get; set; } = string.Empty;
    }
}
