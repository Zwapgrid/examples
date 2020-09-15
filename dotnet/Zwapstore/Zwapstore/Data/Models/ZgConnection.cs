using Newtonsoft.Json;

namespace Zwapstore.Data.Models
{
    internal class ZgConnection
    {
        /// <summary>
        /// This is used in Zwapgrid UI to identify the specific connection amongst others of the same type
        /// Recommended to be the account or company name
        /// </summary>
        [JsonProperty("title", Required = Required.Always)]
        public string Title { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; }
        
        // Use the connection type for your system
        [JsonProperty("invoiceOnline")]
        public InvoiceOnlineConnection InvoiceOnlineConnection { get; set; }
        
        // More connection types will be added here....
    }
}