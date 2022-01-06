using Newtonsoft.Json;

namespace Marketplace.Data.Models
{
    internal class ZgApiResponse<TType>
    {
        [JsonProperty("result")]
        public TType Result { get; set; }
        
        [JsonProperty("success")]
        public bool Success { get; set; }

        public Error Error { get; set; }
    }

    internal class Error
    {
        public string Message { get; set; }
        
        public string Details { get; set; }
    }
}