namespace CredentialPostTest.Data.Models
{
    internal class TokenResponse
    {
        public Response Response { get; set; }
    }
    
    internal class Response
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }
    }
}