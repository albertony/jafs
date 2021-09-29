//
// OpenID Connect (OIDC) 1.0 (based on OAuth 2.0) for Jottacloud.
// Documentation: http://openid.net/connect/
//
using Jottacloud.OAuthData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Jottacloud
{
    public class OAuthAPI
    {
        private const string CLIENT_ID = "jottacli";
        private const string SCOPE = "offline_access+openid";

        private static LoginToken ParseLoginToken(string loginTokenString)
        {
            // Parse Personal Login Token, which is a base64 encoded json string,
            // generated interactively by user in Web GUI at https://www.jottacloud.com/web/secure.
            //var tokenBytes = Convert.FromBase64String(base64Token);
            //var tokenObject = Encoding.UTF8.GetString(tokenBytes);
            using (var stream = new MemoryStream(Convert.FromBase64String(loginTokenString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(LoginToken));
                return (LoginToken)serializer.ReadObject(stream);
            }
        }

        private static OpenidConfiguration RequestOpenidConfiguration(LoginToken loginToken)
        {
            // OpenID Connect request for configuration.
            // Simple Get request without any authentication.
            return JsonAPI.Request<OpenidConfiguration>(loginToken.WellKnownLink, HttpMethod.Get);
        }

        private static TokenObject RequestNewToken(LoginToken loginToken, OpenidConfiguration openidConfiguration)
        {
            // OpenID Connect request for a new token.
            // Throws HttpWebResponse with HttpStatusCode.Unauthorized (401) if login token has already been used.
            // Throws HttpWebResponse with HttpStatusCode.BadRequest (400) if request is invalid for other reasons,
            // e.g. invalid client_id (see OpenidAuthenticationError).
            var data = new Dictionary<string, string> {
                { "client_id", CLIENT_ID },
                { "grant_type", "password" },
                { "scope", SCOPE },
                { "username", loginToken.Username },
                { "password", loginToken.AuthToken },
            };
            return JsonAPI.Request<TokenObject>(openidConfiguration.TokenEndpoint, HttpMethod.Post, data: data);
        }

        private static TokenObject RequestTokenRefresh(TokenObject token)
        {
            // OpenID Connect request for refreshing the access token.
            // Throws HttpWebResponse with HttpStatusCode.BadRequest (400) if request is invalid for other reasons,
            // e.g. invalid client_id (see OpenidAuthenticationError).
            var data = new Dictionary<string, string> {
                { "client_id", CLIENT_ID },
                { "grant_type", "refresh_token" },
                { "refresh_token", token.RefreshToken },
            };
            var url = "https://id.jottacloud.com/auth/realms/jottacloud/protocol/openid-connect/token"; // TODO: Same as openidConfiguration.TokenEndpoint, hard coded for now
            return JsonAPI.Request<TokenObject>(url, HttpMethod.Post, data: data);
        }

        private static UserInfo RequestUserInfo(TokenObject token, OpenidConfiguration openidConfiguration)
        {
            // OpenID Connect request for information about authenticated end-user.
            var headers = new Dictionary<string, string>() { { "Authorization", "Bearer " + token.AccessToken } };
            return JsonAPI.Request<UserInfo>(openidConfiguration.UserinfoEndpoint, HttpMethod.Get, headers: headers);
        }

        public static TokenObject CreateToken(string PersonalLoginToken)
        {
            // Create new token.
            // Input argument must be a Personal Login Token, base64 encoded json string,
            // generated interactively by user from Web GUI at https://www.jottacloud.com/web/secure.
            try
            {
                var loginToken = ParseLoginToken(PersonalLoginToken);
                var openidConfiguration = RequestOpenidConfiguration(loginToken);
                var token = RequestNewToken(loginToken, openidConfiguration);
                return token;
            }
            catch (WebException we)
            {
                HttpWebResponse response = (HttpWebResponse)we.Response;
                /*
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Authorization with Jottacloud failed, please check that you are using the correct username and password, and try again!");
                }
                */
                // If there is an error requesting token from the service, it will return response with
                // HTTP 400 status code, and response content which is a JSON with key "error" with
                // value "invalid_request", "invalid_client", "invalid_grant", "invalid_scope", "unauthorized_client",
                // or "unsupported_grant_type", and optional key "error_description" with more descriptive message,
                // and optional key "error_uri" with url to relevant documentation.
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    /*
                    using (Stream stream = response.GetResponseStream())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(OpenidAuthenticationError));
                        var errorObject (OpenidAuthenticationError)serializer.ReadObject(stream);
                    }
                    */
                    using (Stream stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        string text = reader.ReadToEnd();
                        throw new Exception("New access token request was invalid\n" + text);
                    }
                }
                throw;
            }
        }

        public static TokenObject RefreshToken(TokenObject token)
        {
            // Refresh existing token.
            try
            {
                var newToken = RequestTokenRefresh(token);
                return newToken;
            }
            catch (WebException we)
            {
                HttpWebResponse response = (HttpWebResponse)we.Response;
                /*
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Authorization with Jottacloud failed, please check that you are using the correct username and password, and try again!");
                }
                */
                // If there is an error requesting token from the service, it will return response with
                // HTTP 400 status code, and response content which is a JSON with key "error" with
                // value "invalid_request", "invalid_client", "invalid_grant", "invalid_scope", "unauthorized_client",
                // or "unsupported_grant_type", and optional key "error_description" with more descriptive message,
                // and optional key "error_uri" with url to relevant documentation.
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    using (Stream stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        string text = reader.ReadToEnd();
                        throw new Exception("Token refresh request was invalid\n" + text);
                    }
                }
                throw;
            }
        }

        public static string ExportToken(TokenObject token)
        {
            // Export token to JSON string, in the same format as the OpenID Connect API uses (lowercase member names with underscore).
            using (var memoryStream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(TokenObject));
                serializer.WriteObject(memoryStream, token);
                var tokenBytes = memoryStream.ToArray();
                return Encoding.UTF8.GetString(tokenBytes, 0, tokenBytes.Length);
            }
        }

        public static TokenObject ImportToken(string tokenString)
        {
            // Import token from JSON string, in the same format as the OpenID Connect API uses (lowercase member names with underscore).
            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(tokenString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(TokenObject));
                return (TokenObject)serializer.ReadObject(memoryStream);
            }
        }

    }
}