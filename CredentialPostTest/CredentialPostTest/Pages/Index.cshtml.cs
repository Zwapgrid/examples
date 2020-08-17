using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CredentialPostTest.Data;
using CredentialPostTest.Utilities;
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
        private readonly string _zgApiUrl;
        private readonly string _zgAppUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _accessToken;

        public IndexModel(
            ILogger<IndexModel> logger,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _logger = logger;
            _userManager = userManager;
            _zgApiUrl = configuration.GetValue<string>("Zwapgrid:ApiUrl");
            _zgAppUrl = configuration.GetValue<string>("Zwapgrid:AppUrl");
            _clientId = configuration.GetValue<string>("Zwapgrid:ClientId");
            _clientSecret = configuration.GetValue<string>("Zwapgrid:ClientSecret");
            _accessToken = configuration.GetValue<string>("Zwapgrid:AccessToken");
        }

        public async Task OnGet()
        {
            const string sourceSystem = "InvoiceOnline";
            
            //Check that user is logged in
            if(!User.Identity.IsAuthenticated)
                return;
          
            //Fetch user
            var user = await _userManager.GetUserAsync(User);
            
            //Fetch one time code, using client id and secret
            var otc = await GetOneTimeCodeAsync();
            
            //Check if user already have an active connection
            if (!user.ZgConnectionId.HasValue)
            {
                //Create connection and store connectionId
                var connectionId = await CreateConnection(user.CompanyName, {userSecretKey}, {userStoreId}, otc);
                
                user.ZgConnectionId = connectionId;
                await _userManager.UpdateAsync(user);
            }

            //Encrypt connectionId for usage in query
            var encryptedConnectionId = await GetCalculatedId(user.ZgConnectionId.Value, otc);

            //Add hide source to hide the connection in the view, this is recommended to give a better experience for user
            var hideSource = true.ToString();

            //Place user info in query
            IFrameUrl = $"{_zgAppUrl}zwapstore?otc={otc}&name={user.CompanyName}&orgno={user.CompanyOrgNo}&email={user.Email}&sourceConnectionId={encryptedConnectionId}&source={sourceSystem}&hideSource={hideSource}";
        }

        [BindProperty]
        public string IFrameUrl { get; set; }
        
        async Task<int> CreateConnection(string title, string secretKey, string storeId, string otc)
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
        private async Task<string> GetCalculatedId(int connectionId, string otc)
        {
            var rsaParameters = await GetRsaParameters();
            
            var toEncrypt = $"{connectionId}||{otc}";

            using var cryptoServiceProvider = new RSACryptoServiceProvider(PublicKeySize);
            cryptoServiceProvider.ImportParameters(rsaParameters);
            var encryptedBytes = cryptoServiceProvider.Encrypt(Encoding.UTF8.GetBytes(toEncrypt), false);
            return Base64UrlEncoder.Encode(encryptedBytes);
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
            string responseContent;
            using (var restClient = new HttpClient())
            {
                using(var request = new HttpRequestMessage(HttpMethod.Get, $"{_zgApiUrl}api/v1{endpoint}"))
                {
                    request.Headers.Add("Authorization", "Bearer " + _accessToken);

                    var response = await restClient.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResult>>(responseContent);
            
            return responseObject.Result;
        }
        
        private async Task<TResult> Post<TInput, TResult>(TInput input, string endpoint)
        {
            string responseContent;
            using (var restClient = new HttpClient())
            {
                using(var request = new HttpRequestMessage(HttpMethod.Post, $"{_zgApiUrl}api/v1/connections{endpoint}")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json")
                })
                {
                    request.Headers.Add("Authorization", "Bearer " + _accessToken);

                    var response = await restClient.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResult>>(responseContent);
            
            return responseObject.Result;
        }

        private async Task<string> GetOneTimeCodeAsync()
        {
            var otcRequest = new OneTimeCodeRequest
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
            };

            string responseContent;
            using (var restClient = new HttpClient())
            {
                using(var request = new HttpRequestMessage(HttpMethod.Post, $"{_zgApiUrl}api/zwapstore/one-time-code")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(otcRequest), Encoding.UTF8, "application/json")
                })
                {
                    var response = await restClient.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<OneTimeCodeResponse>>(responseContent);

            return responseObject?.Result.OneTimeCode;
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

    internal class OneTimeCodeRequest
    {
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }
    }
    
    internal class OneTimeCodeResponse
    {
        public string OneTimeCode { get; set; }
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
        
        // Use the connection type for your system
        [JsonProperty("invoiceOnline")]
        public InvoiceOnlineConnection InvoiceOnlineConnection { get; set; }
        
        // More connection types will be added here....
    }

    internal class InvoiceOnlineConnection
    {        
        [JsonProperty("secretKey", Required = Required.Always)]
        public string SecretKey { get; set; }
            
        [JsonProperty("storeId", Required = Required.Always)]
        public string StoreId { get; set; }
    }
}
