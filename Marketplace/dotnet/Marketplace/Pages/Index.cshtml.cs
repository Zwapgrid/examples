using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Marketplace.Data;
using Marketplace.Data.Models;

namespace Marketplace.Pages
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

            if(!User.Identity.IsAuthenticated)
            {
                return;
            }

            //Get the current user
            _user = await _userManager.GetUserAsync(User);
            if (_user == null)
            {
                throw new Exception("User does not exist");
            }

            // To authenticate your client when using Marketplace without exposing your client secret in the iFrame
            // URL, you need to get a one-time code (OTC) by making a request to Zwapgrid API each time you embed the
            // Marketplace.
            _otc = await GetOneTimeCodeAsync();

            // For Zwapgrid to be able to access your API, you need to provide the credentials by creating a
            // "Connection". These credentials are of course different depending on how your system is implemented,
            // this is just an example.
            if (!_user.ZgConnectionId.HasValue)
            {
                // These should be unique for the user or account of your system. This example has credentials with a
                // "secretKey" and a "storeId", but the credentials for your system is probably different.
                const string userSecretKey = "{secretKey}";
                const string userStoreId = "{storeId}";

                // Create connection and store connectionId
                var connectionId = await CreateConnection(_user.CompanyName, _user.CompanyId, userSecretKey, userStoreId);

                // Store the connectionId for later use, so you don't have to create new connection every time.
                _user.ZgConnectionId = connectionId;
                await _userManager.UpdateAsync(_user);
            }

            // Add hideSource query parameter to hide your system in the view, this is recommended to give a better
            // experience for user. Since they then only see the "other" system they are connecting to.
            var hideSource = true.ToString();

            // Build url from user info and credentials
            IFrameUrl = $"{_zgAppUrl}marketplace?" +
                $"otc={_otc}" +
                $"&name={_user.CompanyName}" +
                $"&companyId={_user.CompanyId}" +
                $"&email={_user.Email}" +
                $"&tenancyName={_user.CompanyName}" +
                $"&export.connectionId={_user.ZgConnectionId.Value}" +
                $"&source={sourceSystem}" +
                $"&hideSource={hideSource}" +
                $"&lang=sv"; // examples: 'en', 'sv'
        }

        // When an account has been created or the user has authorized you to access their account, a JS event will
        // be triggered with key 'client.authenticated'. You should register a listener to this and handle the
        // authorizationCode provided. Exchange it for AccessToken and RefreshToken if you want to access the users'
        // account at a later time. Check site.js for example.
        public async Task<IActionResult> OnGetClientAuthenticated(string authorizationCode)
        {
            var request = new Oauth2Request
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
                Code = authorizationCode,
                GrantType = "authorization_code"
            };
            var result = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token");

            if (result?.Response != null && User != null)
            {
                var user = await _userManager.GetUserAsync(User);
                await UpdateUserTokensAsync(user, result.Response.AccessToken, result.Response.RefreshToken);
            }

            return new JsonResult(new
                { AccessToken = result?.Response?.AccessToken, EncryptedAccessToken = result?.Response?.EncryptedAccessToken });
        }

        /// <summary>
        /// Get access token using refresh token, stored for currently logged in user
        /// Be aware that you cannot use the same refresh token more than once, if you do so, you will get 401
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnGetRefreshAccessToken()
        {
            if (User == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.GetUserAsync(User);
            var request = new Oauth2Request
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
                RefreshToken = user.RefreshToken,
                GrantType = "refresh_token"
            };

            var result = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token");

            if (result?.Response != null)
            {
                //Updating user tokens here, since refresh token is valid only for 1 request
                await UpdateUserTokensAsync(user, result.Response.AccessToken, result.Response.RefreshToken);
            }

            return new JsonResult(new
                { AccessToken = result?.Response?.AccessToken, EncryptedAccessToken = result?.Response?.EncryptedAccessToken });
        }

        [BindProperty]
        public string IFrameUrl { get; set; }

        async Task<int> CreateConnection(string title, string orgNo, string secretKey, string storeId)
        {
            var createModel = new ZgConnection
            {
                Title = title,
                CompanyId = orgNo,
                InvoiceOnlineConnection = new InvoiceOnlineConnection
                {
                    SecretKey = secretKey,
                    StoreId = storeId
                }
            };

            var connectionResult = await Post<ZgConnection, ZgConnection>(createModel, "connections", "");

            if(string.IsNullOrEmpty(connectionResult.Id))
                throw new Exception("Something went wrong");

            return Convert.ToInt32(connectionResult.Id);
        }

        private async Task<TResult> Post<TInput, TResult>(TInput input, string endpoint, string action)
        {
            string responseContent;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true
            };
            using (var restClient = new HttpClient(handler))
            {
                var content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json");
                var response = await GetResponseMessageAsync(HttpMethod.Post, $"{_zgApiUrl}api/v1/{endpoint}/{action}", content, restClient);

                responseContent = await response.Content.ReadAsStringAsync();
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

            var response = await Post<OneTimeCodeRequest, OneTimeCodeResponse>(otcRequest, "oauth2","one-time-code");

            return response?.OneTimeCode;
        }

        private Task UpdateUserTokensAsync(ApplicationUser user, string accessToken, string refreshToken)
        {
            user.AccessToken = accessToken;
            user.RefreshToken = refreshToken;
            return _userManager.UpdateAsync(user);
        }

        private async Task Handle401Async()
        {
            if (_user == null)
            {
                return;
            }

            // If no refresh token, clear access token and exit
            if (string.IsNullOrEmpty(_user.RefreshToken))
            {
                _user.AccessToken = string.Empty;
                return;
            }

            var request = new Oauth2Request
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
                RefreshToken = _user.RefreshToken,
                GrantType = "refresh_token"
            };

            var response = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token");
            _user.AccessToken = response?.Response?.AccessToken;
            _user.RefreshToken = response?.Response?.RefreshToken;

            await _userManager.UpdateAsync(_user);
        }

        private async Task<HttpResponseMessage> GetResponseMessageAsync(HttpMethod method, string url, HttpContent content, HttpClient client)
        {
            Task<HttpResponseMessage> SendAsync()
            {
                var request = new HttpRequestMessage(method, url);
                if (content != null)
                {
                    request.Content = content;
                }

                AddAuthorizationHeader(request);

                return client.SendAsync(request);
            }

            var response = await SendAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await Handle401Async();
            }
            else
            {
                return response;
            }

            response = await SendAsync();

            return response;
        }

        private void AddAuthorizationHeader(HttpRequestMessage request)
        {
            request.Headers.Remove("Authorization");

            if (_user != null && !string.IsNullOrEmpty(_user.AccessToken))
            {
                request.Headers.Add("Authorization", "Bearer " + _user.AccessToken);
            }
            else if (!string.IsNullOrEmpty(_otc))
            {
                request.Headers.Add("Authorization", "OneTimeCode " + _otc);
            }
        }
    }
}
