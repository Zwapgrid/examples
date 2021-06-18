using Newtonsoft.Json;

namespace Marketplace.Data.Models
{
    internal class ZgValidatePostResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }
        
        [JsonProperty("value")]
        public string Value { get; set; }
    }
}