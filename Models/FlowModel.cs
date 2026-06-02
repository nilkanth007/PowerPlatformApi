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

        // Microsoft List Schema properties
        [JsonProperty("objectId")]
        public string ObjectId { get; set; } = string.Empty;

        [JsonProperty("objectName")]
        public string ObjectName { get; set; } = string.Empty;

        [JsonProperty("objectCreated")]
        public System.DateTime? ObjectCreated { get; set; }

        [JsonProperty("objectModified")]
        public System.DateTime? ObjectModified { get; set; }

        [JsonProperty("objectLink")]
        public string ObjectLink { get; set; } = string.Empty;

        [JsonProperty("objectType")]
        public string ObjectType { get; set; } = "Flow";

        [JsonProperty("environmentName")]
        public string EnvironmentName { get; set; } = string.Empty;
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
