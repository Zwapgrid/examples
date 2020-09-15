namespace Zwapstore.Data.Models
{
    internal class Oauth2Response
    {
        public Response Response { get; set; }
    }
    
    internal class Response
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }
    }
}