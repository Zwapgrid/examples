namespace CredentialPostTest.Data.Models
{
    public class AccessTokenRequest : ZwapstoreRequest
    {
        public string Code { get; set; }

        public string RedirectUri { get; set; }
    }
}