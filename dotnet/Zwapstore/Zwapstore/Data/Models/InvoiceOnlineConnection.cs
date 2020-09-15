using Newtonsoft.Json;

namespace Zwapstore.Data.Models
{
    internal class InvoiceOnlineConnection
    {        
        [JsonProperty("secretKey", Required = Required.Always)]
        public string SecretKey { get; set; }
            
        [JsonProperty("storeId", Required = Required.Always)]
        public string StoreId { get; set; }
    }
}