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
        private const string SCOPE = "openid offline_access";

        private static LoginToken ParseLoginToken(string loginTokenString)
        {
            // Parse Personal Login Token, generated interactively by user in Web GUI at
            // https://www.jottacloud.com/web/secure.
            // This is a JSON string encoded as raw base64url, i.e. using the alternate
            // URL- and filesystem-safe alphabet which substitutes '-' instead of '+'
            // and '_' instead of '/' in the standard Base64 alphabet, and without padding.
            // The Convert.FromBase64String assumes standard base64 encoding, with padding,
            // so we must convert manually.
            var tokenBytes = Convert.FromBase64String(loginTokenString.PadRight(loginTokenString.Length + loginTokenString.Length * 3 % 4, '=').Replace("-", "+").Replace("_", "/"));
            using (var stream = new MemoryStream(tokenBytes))
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
            // Input argument must be a Personal Login Token, json string encoded as raw base64url,
            // generated interactively by user from Web GUI at https://www.jottacloud.com/web/secure.
            LoginToken loginToken;
            try
            {
                loginToken = ParseLoginToken(PersonalLoginToken);
            }
            catch
            {
                throw new ArgumentException("Value is not a valid Personal Login Token");
            }
            OpenidConfiguration openidConfiguration;
            try
            {
                openidConfiguration = RequestOpenidConfiguration(loginToken);
            }
            catch
            {
                throw new Exception("Request for OpenID Configuration using supplied login token failed");
            }
            try
            {
                return RequestNewToken(loginToken, openidConfiguration);
            }
            catch (WebException webException)
            {
                throw OAuthException.CreateFromWebException(webException, "Request for new token failed");
            }
        }

        public static TokenObject RefreshToken(TokenObject token)
        {
            // Refresh existing token.
            try
            {
                return RequestTokenRefresh(token);
            }
            catch (WebException webException)
            {
                throw OAuthException.CreateFromWebException(webException, "Request for token refresh failed");
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
            try
            {
                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(tokenString)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(TokenObject));
                    return (TokenObject)serializer.ReadObject(memoryStream);
                }
            }
            catch
            {
                throw new ArgumentException("Value is not a valid token object");
            }
        }
    }

    public class OAuthException : Exception
    {
        public OAuthException(string message) : base(message) { }
        public static OAuthException CreateFromWebException(WebException webException, string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = webException.Message;
            }
            HttpWebResponse response = (HttpWebResponse)webException.Response;
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new OAuthBadRequestException(message, response);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                //throw new UnauthorizedAccessException("Authorization with Jottacloud failed, please check that you are using the correct username and password, and try again!");
                return new OAuthUnauthorizedException(message);
            }
            else
            {
                return new OAuthException(message);
            }
        }
    }

    public class OAuthUnauthorizedException : OAuthException
    {
        public OAuthUnauthorizedException(string message) : base(message) {}
    }

    public class OAuthBadRequestException : OAuthException
    {
        public OpenidAuthenticationError OAuthResponseObject { get; }
        public string OAuthResponseString { get; }
        public OAuthBadRequestException(string message, OpenidAuthenticationError errorObject) : base(message)
        {
            OAuthResponseObject = errorObject;
        }
        public OAuthBadRequestException(string message, HttpWebResponse response) : base(message)
        {
            try
            {
                // If there is an error requesting token from the service, it will return response with
                // HTTP 400 status code, and response content which is a JSON with key "error" with
                // value "invalid_request", "invalid_client", "invalid_grant", "invalid_scope", "unauthorized_client",
                // or "unsupported_grant_type", and optional key "error_description" with more descriptive message,
                // and optional key "error_uri" with url to relevant documentation.
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(OpenidAuthenticationError));
                        OAuthResponseObject = (OpenidAuthenticationError)serializer.ReadObject(stream);
                    }
                }
                else
                {
                    using (Stream stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        OAuthResponseString = reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                OAuthResponseString = "Failed to parse OAuth response";
            }
        }
        public override string ToString()
        {
            string result = Message;
            if (OAuthResponseObject != null)
            {
                result += $" ({OAuthResponseObject.Code}: {OAuthResponseObject.Description})";
            }
            else if (!string.IsNullOrEmpty(OAuthResponseString))
            {
                result += $" ({OAuthResponseString})";
            }
            else
            {
                result += " (OAuth bad request)";
            }
            return result;
        }
    }
}