using Newtonsoft.Json;

namespace PowerPlatform.Api.Models
{
    public class EnvironmentModel
    {
        [JsonProperty("environmentId")]
        public string EnvironmentId { get; set; } = string.Empty;

        [JsonProperty("environmentName")]
        public string EnvironmentName { get; set; } = string.Empty;

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }
}
