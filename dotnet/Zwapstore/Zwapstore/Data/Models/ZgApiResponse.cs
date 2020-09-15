using Newtonsoft.Json;

namespace Zwapstore.Data.Models
{
    internal class ZgApiResponse<TType>
    {
        [JsonProperty("result")]
        public TType Result { get; set; }
        
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}