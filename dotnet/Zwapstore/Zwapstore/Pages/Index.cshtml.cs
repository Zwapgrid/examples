﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Zwapstore.Data;
using Zwapstore.Data.Models;
using Zwapstore.Utilities;

namespace Zwapstore.Pages
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
            {
                return;
            }
            
            //Fetch user
            _user = await _userManager.GetUserAsync(User);
            if (_user == null)
            {
                throw new Exception("User does not exist");
            }

            //Fetch one time code (OTC), using client id and secret
            //OTC is used to handle authentication when access token is missing in Zwapstore
            _otc = await GetOneTimeCodeAsync();
            
            //Check if user already has an active connection
            if (!_user.ZgConnectionId.HasValue)
            {
                //Create connection and store connectionId
                var connectionId = await CreateConnection(_user.CompanyName, _user.CompanyOrgNo, {secretKey}, {storeId});
                
                _user.ZgConnectionId = connectionId;
                await _userManager.UpdateAsync(_user);
            }

            //Add hide source to hide the connection in the view, this is recommended to give a better experience for user
            var hideSource = true.ToString();

            //Place user info in query
            IFrameUrl = $"{_zgAppUrl}zwapstore?otc={_otc}&name={_user.CompanyName}&orgno={_user.CompanyOrgNo}&email={_user.Email}&tenancyName={_user.CompanyName}&clientId={_clientId}&sourceConnectionId={_user.ZgConnectionId.Value}&source={sourceSystem}&hideSource={hideSource}";
        }

        //Get access token using auth code, sent from iframe
        public async Task<IActionResult> OnGetAccessToken(string authCode)
        {
            var request = new Oauth2Request
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
                Code = authCode,
                GrantType = "authorization_code"
            };
            var result = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token", false);

            if (result?.Response != null && User != null)
            {
                var user = await _userManager.GetUserAsync(User);
                await UpdateUserTokensAsync(user, result.Response.AccessToken, result.Response.RefreshToken);
            }

            return new JsonResult(new { AccessToken = result?.Response?.AccessToken });
        }

        //Get access token using refresh token, stored for currently logged in user
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
            
            var result = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token", false);

            if (result?.Response != null)
            {
                //Updating user tokens here, since refresh token is valid only for 1 request
                await UpdateUserTokensAsync(user, result.Response.AccessToken, result.Response.RefreshToken);
            }
            
            return new JsonResult(new { AccessToken = result?.Response?.AccessToken });
        }
        
        [BindProperty]
        public string IFrameUrl { get; set; }

        async Task<int> CreateConnection(string title, string orgNo, string secretKey, string storeId)
        {
            var createModel = new ZgConnection
            {
                Title = title,
                OrgNo = orgNo,
                InvoiceOnlineConnection = new InvoiceOnlineConnection
                {
                    SecretKey = secretKey,
                    StoreId = storeId
                }
            };

            var connectionResult = await Post<ZgConnection, ZgConnection>(createModel, "connections", "");

            if(string.IsNullOrEmpty(connectionResult.Id))
                throw new Exception("Something went wrong");
            
            var validateConnectionResult = await Post<ZgConnection, ZgValidatePostResult>(connectionResult, "connections", "validate");

            if (validateConnectionResult.Success)
            {
                return Convert.ToInt32(connectionResult.Id);
            }

            throw new Exception(validateConnectionResult.Message);
        }

        private async Task<TResult> Get<TResult>(string endpoint, bool withAuthorization) where TResult : class
        {
            string responseContent;
            using (var restClient = new HttpClient())
            {
                var response = await GetResponseMessageAsync(HttpMethod.Get, $"{_zgApiUrl}api/v1{endpoint}", null, restClient, withAuthorization);
                responseContent = await response.Content.ReadAsStringAsync();
            }

            var responseObject = JsonConvert.DeserializeObject<ZgApiResponse<TResult>>(responseContent);
            
            return responseObject.Result;
        }
        
        private async Task<TResult> Post<TInput, TResult>(TInput input, string endpoint, string action, bool withAuthorization = true)
        {
            string responseContent;
            using (var restClient = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json");
                var response = await GetResponseMessageAsync(HttpMethod.Post, $"{_zgApiUrl}api/v1/{endpoint}/{action}", content, restClient, withAuthorization);

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
                
            var response = await Post<Oauth2Request, Oauth2Response>(request, "oauth2", "token", false);
            _user.AccessToken = response?.Response?.AccessToken;
            _user.RefreshToken = response?.Response?.RefreshToken;

            await _userManager.UpdateAsync(_user);
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

        private async Task<HttpResponseMessage> GetResponseMessageAsync(HttpMethod method, string url, HttpContent content, HttpClient client, bool withAuthorization = true)
        {
            Task<HttpResponseMessage> SendAsync()
            {
                var request = new HttpRequestMessage(method, url);
                if (content != null)
                {
                    request.Content = content;
                }
                
                if (withAuthorization)
                {
                    AddAuthorizationHeader(request);
                }

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
    }
}
