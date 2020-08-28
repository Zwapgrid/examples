namespace CredentialPostTest.Data.Models
{
    internal class OauthRequest
    {
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }
        
        public string RefreshToken { get; set; }
        
        public string Code { get; set; }

        public string RedirectUri { get; set; }

        public string GrantType { get; set; }
    }
}