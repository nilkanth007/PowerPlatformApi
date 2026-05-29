using Newtonsoft.Json;

namespace PowerPlatform.Api.Models
{
    public class FlowModel
    {
        [JsonProperty("flowId")]
        public string FlowId { get; set; } = string.Empty;

        [JsonProperty("flowName")]
        public string FlowName { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string State { get; set; } = string.Empty;
    }
    
    public class FlowDefinitionModel
    {
        [JsonProperty("properties")]
        public FlowDefinitionProperties? Properties { get; set; }
    }

    public class FlowDefinitionProperties
    {
        [JsonProperty("definition")]
        public object? Definition { get; set; }
    }
}
