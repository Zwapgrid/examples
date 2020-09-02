namespace CredentialPostTest.Data.Models
{
    internal class RefreshTokenRequest
    {
        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string RefreshToken { get; set; }
    }
}