using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CredentialPostTest.Data;
using CredentialPostTest.Data.Models;
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

        private ApplicationUser _user;
        
        private string _otc;
        
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
        }

        public async Task OnGet()
        {
            const string sourceSystem = "InvoiceOnline";
            
            //Check that user is logged in
            if(!User.Identity.IsAuthenticated)
                return;
          
            //Fetch user
            _user = await _userManager.GetUserAsync(User);
            if (_user == null)
            {
                throw new Exception("User does not exist");
            }

            //Fetch one time code, using client id and secret
            _otc = await GetOneTimeCodeAsync();
            
            //Check if user already have an active connection
            if (!_user.ZgConnectionId.HasValue)
            {
                //Create connection and store connectionId
                var connectionId = await CreateConnection(_user.CompanyName, {userSecretKey}, {userStoreId});
                
                _user.ZgConnectionId = connectionId;
                await _userManager.UpdateAsync(_user);
            }

            //Encrypt connectionId for usage in query
            var encryptedConnectionId = await GetCalculatedId(_user.ZgConnectionId.Value);

            //Add hide source to hide the connection in the view, this is recommended to give a better experience for user
            var hideSource = true.ToString();

            //Place user info in query
            IFrameUrl = $"{_zgAppUrl}zwapstore?otc={_otc}&name={_user.CompanyName}&orgno={_user.CompanyOrgNo}&email={_user.Email}&sourceConnectionId={encryptedConnectionId}&source={sourceSystem}&hideSource={hideSource}";
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
            
            var toEncrypt = $"{connectionId}||{_otc}";

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
                    var response = await GetResponseMessageAsync(request, restClient);

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
                    var response = await GetResponseMessageAsync(request, restClient);

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

            var response = await CallZwapstoreAsync<OneTimeCodeRequest, OneTimeCodeResponse>(otcRequest, "one-time-code");

            return response?.OneTimeCode;
        }

        private async Task Handle401Async()
        {
            // If no refresh token, clear access token and exit
            if (string.IsNullOrEmpty(_user.RefreshToken))
            {
                _user.AccessToken = string.Empty;
                return;
            }
            else
            {
                var request = new RefreshTokenRequest
                {
                    ClientId = _clientId,
                    ClientSecret = _clientSecret,
                    RefreshToken = _user.RefreshToken
                };
                
                var response = await CallZwapstoreAsync<RefreshTokenRequest, RefreshTokenResponse>(request, "refresh-token");
                _user.AccessToken = response?.AccessToken;
                _user.RefreshToken = response?.RefreshToken;
            }

            await _userManager.UpdateAsync(_user);
        }
        
        private async Task<TResponse> CallZwapstoreAsync<TRequest, TResponse>(TRequest requestModel, string endpoint) where TResponse: class
        {
            string responseContent;
            using (var restClient = new HttpClient())
            {
                using(var request = new HttpRequestMessage(HttpMethod.Post, $"{_zgApiUrl}api/zwapstore/{endpoint}")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(requestModel), Encoding.UTF8, "application/json")
                })
                {
                    var response = await GetResponseMessageAsync(request, restClient);

                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResponse>>(responseContent);

            return responseObject?.Result;
        }
        
        private void AddAuthorizationHeader(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(_user.AccessToken))
            {
                request.Headers.Add("Authorization", "Bearer " + _user.AccessToken);
            }
            else if (!string.IsNullOrEmpty(_otc))
            {
                request.Headers.Add("Authorization", "OneTimeCode " + _otc);
            }
        }

        private async Task<HttpResponseMessage> GetResponseMessageAsync(HttpRequestMessage request, HttpClient client)
        {
            AddAuthorizationHeader(request);

            var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await Handle401Async();
            }
            else
            {
                return response;
            }
            
            AddAuthorizationHeader(request);
            
            response = await client.SendAsync(request);

            return response;
        }
    }
}
