using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CredentialPostTest.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace CredentialPostTest.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string _partnerToken;
        private readonly string _zgApiUrl;
        private readonly string _zgAppUrl;

        public IndexModel(
            ILogger<IndexModel> logger,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _logger = logger;
            _userManager = userManager;
            _partnerToken = configuration.GetValue<string>("Zwapgrid:PartnerToken");
            _zgApiUrl = configuration.GetValue<string>("Zwapgrid:ApiUrl");
            _zgAppUrl = configuration.GetValue<string>("Zwapgrid:AppUrl");
        }

        public async Task OnGet()
        {
            //Check that user is logged in
            if(!User.Identity.IsAuthenticated)
                return;
          
            //Fetch user
            var user = await _userManager.GetUserAsync(User);
            
            //Check if user already have an active connection
            if (!user.ZgConnectionId.HasValue)
            {
                //Create connection and store connectionId and publicKey in db
                var connectionId = await CreateConnection(
                    user.CompanyName, 
                    "176165196217", 
                    "18920");
                
                user.ZgConnectionId = connectionId;
                await _userManager.UpdateAsync(user);
            }

            //Encrypt connectionId for usage in query
            var encryptedConnectionId = await GetCalculatedId(user.ZgConnectionId.Value);
            
            //Place user info in query
            IFrameUrl = $"{_zgAppUrl}zwapstore?token={_partnerToken}&name={user.CompanyName}&orgno={user.CompanyOrgNo}&email={user.Email}&sourceConnectionId={encryptedConnectionId}";
        }

        [BindProperty]
        public string IFrameUrl { get; set; }


        async Task<int> CreateConnection(string title, string secretKey, string storeId)
        {
            var createModel = new ZgConnection
            {
                Title = title,
                InvoiceOnlineConnection = new InvoiceOnlineConnection
                {
                    SecretKey = secretKey,
                    StoreId = storeId
                }
            };

            var connectionResult = await Post<ZgConnection, ZgConnection>(createModel, "");

            if(string.IsNullOrEmpty(connectionResult.Id))
                throw new Exception("Something went wrong");
            
            var validateConnectionResult = await Post<ZgConnection, ZgValidatePostResult>(connectionResult,"/validate");

            if (validateConnectionResult.Success)
            {
                return Convert.ToInt32(connectionResult.Id);
            }

            throw new Exception(validateConnectionResult.Message);
        }

        private const int PublicKeySize = 4096;
        private async Task<string> GetCalculatedId(int connectionId)
        {
            var rsaParameters = await GetRsaParameters();
            
            var toEncrypt = $"{connectionId}||{_partnerToken}";

            using var cryptoServiceProvider = new RSACryptoServiceProvider(PublicKeySize);
            cryptoServiceProvider.ImportParameters(rsaParameters);
            var encryptedBytes = cryptoServiceProvider.Encrypt(Encoding.UTF8.GetBytes(toEncrypt), false);
            return Convert.ToBase64String(encryptedBytes);
        }

        private async Task<RSAParameters> GetRsaParameters()
        {
            var publicKey = await Get<string>("/me/public-key");
            
            var utf8Mark = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

            if (publicKey.StartsWith(utf8Mark, StringComparison.Ordinal))
                publicKey = publicKey.Remove(0, utf8Mark.Length);

            using var reader = new StringReader(publicKey);
            var pemObject = new PemReader(reader).ReadObject();
            if (pemObject is RsaKeyParameters parameters)
            {
                return DotNetUtilities.ToRSAParameters(parameters);
            }
            
            throw new Exception("Something went wrong");
        }

        private async Task<TResult> Get<TResult>(string endpoint) where TResult : class
        {
            var restClient = new HttpClient();
            
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_zgApiUrl}api/v1{endpoint}");
            
            request.Headers.Add("Authorization", $"Partner {_partnerToken}");
            
            var response = await restClient.SendAsync(request);
            
            var responseContent = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResult>>(responseContent);
            
            return responseObject.Result;
        }
        
        private async Task<TResult> Post<TInput, TResult>(TInput input, string endpoint)
        {
            var restClient = new HttpClient();

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_zgApiUrl}api/v1/connections{endpoint}")
            {
                Content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Partner {_partnerToken}");
            
            var response = await restClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResult>>(responseContent);
            
            return responseObject.Result;
        }
    }

    internal class ZgApiResponse<TType>
    {
        [JsonProperty("result")]
        public TType Result { get; set; }
        
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("error")]
        public string Error { get; set; }
    }
    
    internal class ZgValidatePostResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }
        
        [JsonProperty("value")]
        public string Value { get; set; }
    }

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

        [JsonProperty("invoiceOnline")]
        public InvoiceOnlineConnection InvoiceOnlineConnection { get; set; }
    }

    internal class InvoiceOnlineConnection
    {
        
        //Properties below are different depending on your specific credentials,
        //contact Zwapgrid for more info about your specific creds
        [JsonProperty("secretKey", Required = Required.Always)]
        public string SecretKey { get; set; }
            
        [JsonProperty("storeId", Required = Required.Always)]
        public string StoreId { get; set; }
    }
}