using System;

namespace Marketplace.Utilities
{
    // Source: https://jonlabelle.com/snippets/view/csharp/base64-url-encode-and-decode
    public static class Base64UrlEncoder
    {
        public static string Encode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
 
            output = output.Split('=')[0]; // Remove any trailing '='s
            output = output.Replace('+', '-'); // 62nd char of encoding
            output = output.Replace('/', '_'); // 63rd char of encoding
 
            return output;
        }
    }
}