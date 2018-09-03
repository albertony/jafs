//
// OpenID Connect (OIDC) 1.0 (based on OAuth 2.0) for Jottacloud.
// Documentation: http://openid.net/connect/
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace JaFS
{

    [DataContract]
    public class OpenidAuthenticationError
    {
        // If there is an error requesting token from the service, it will throw HttpWebResponse
        // with status code HttpStatusCode.BadRequest (400), and response body will be a JSON with
        // key "error" with value "invalid_request", "invalid_client", "invalid_grant", "invalid_scope",
        // "unauthorized_client", or "unsupported_grant_type", and optional key "error_description" with
        // more descriptive message, and optional key "error_uri" with url to relevant documentation.
        [DataMember(Name = "error")] public string Code { get; set; }
        [DataMember(Name = "error_description")] public string Description { get; set; }
        [DataMember(Name = "error_uri")] public string uri { get; set; }
    }

    [DataContract]
    public class LoginToken
    {
        [DataMember(Name = "username")] public string Username { get; set; }
        [DataMember(Name = "realm")] public string Realm{ get; set; }
        [DataMember(Name = "well_known_link")] public string WellKnownLink { get; set; }
        [DataMember(Name = "auth_token")] public string AuthToken { get; set; }
    }

    [DataContract]
    public class OpenidConfiguration
    {
        [DataMember(Name = "issuer")] public string Issuer { get; set; }
        [DataMember(Name = "authorization_endpoint")] public string AuthorizationEndpoint { get; set; }
        [DataMember(Name = "token_endpoint")] public string TokenEndpoint { get; set; }
        [DataMember(Name = "token_introspection_endpoint")] public string TokenIntrospectionEndpoint { get; set; }
        [DataMember(Name = "userinfo_endpoint")] public string UserinfoEndpoint { get; set; }
        [DataMember(Name = "end_session_endpoint")] public string EndSessionEndpoint { get; set; }
        [DataMember(Name = "jwks_uri")] public string JwksURI { get; set; }
        [DataMember(Name = "check_session_iframe")] public string CheckSessionIframe { get; set; }
        // TODO:
        /*
		GrantTypesSupported                        []string `json:"grant_types_supported"`
		ResponseTypesSupported                     []string `json:"response_types_supported"`
		SubjectTypesSupported                      []string `json:"subject_types_supported"`
		IDTokenSigningAlgValuesSupported           []string `json:"id_token_signing_alg_values_supported"`
		UserinfoSigningAlgValuesSupported          []string `json:"userinfo_signing_alg_values_supported"`
		RequestObjectSigningAlgValuesSupported     []string `json:"request_object_signing_alg_values_supported"`
		ResponseNodesSupported                     []string `json:"response_modes_supported"`
		RegistrationEndpoint                       string   `json:"registration_endpoint"`
		TokenEndpointAuthMethodsSupported          []string `json:"token_endpoint_auth_methods_supported"`
		TokenEndpointAuthSigningAlgValuesSupported []string `json:"token_endpoint_auth_signing_alg_values_supported"`
		ClaimsSupported                            []string `json:"claims_supported"`
		ClaimTypesSupported                        []string `json:"claim_types_supported"`
		ClaimsParameterSupported                   bool     `json:"claims_parameter_supported"`
		ScopesSupported                            []string `json:"scopes_supported"`
		RequestParameterSupported                  bool     `json:"request_parameter_supported"`
		RequestURIParameterSupported               bool     `json:"request_uri_parameter_supported"`
		CodeChallengeMethodsSupported              []string `json:"code_challenge_methods_supported"`
		TLSClientCertificateBoundAccessTokens      bool     `json:"tls_client_certificate_bound_access_tokens"`
		IntrospectionEndpoint                      string   `json:"introspection_endpoint"`
         */
    }

    [DataContract]
    public class TokenObject
    {
        [DataMember(Name = "access_token")] public string AccessToken { get; set; }
        [DataMember(Name = "expires_in")] public int ExpiresIn { get; set; } // Expiration time of the Access Token in seconds since it was created.
        [DataMember(Name = "refresh_expires_in")] public int RefreshExpiresIn { get; set; } // Expiration time of the Refresh Token in seconds it was created, typically 0 because the Refresh Token never expires.
        [DataMember(Name = "refresh_token")] public string RefreshToken { get; set; }
        [DataMember(Name = "token_type")] public string TokenType { get; set; }
        [DataMember(Name = "id_token")] public string IdToken{ get; set; }
        [DataMember(Name = "not-before-policy")] public int NotBeforePolicy { get; set; }
        [DataMember(Name = "session_state")] public string SessionState { get; set; }
        [DataMember(Name = "scope")] public string Scope { get; set; }
    }

    [DataContract]
    public class UserInfo
    {
        [DataMember(Name = "sub")] public string Subject { get; set; }
        [DataMember(Name = "email_verified")] public bool EmailVerified { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "realm")] public string Realm { get; set; }
        [DataMember(Name = "preferred_username")] public string PreferredUsername { get; set; } // The numeric internal username, same as Username
        [DataMember(Name = "given_name")] public string GivenName { get; set; }
        [DataMember(Name = "family_name")] public string FamilyName { get; set; }
        [DataMember(Name = "email")] public string Email { get; set; }
        [DataMember(Name = "username")] public string Username { get; set; } // The numeric internal username, same as PreferredUsername
    }

    [DataContract]
    public class CustomerInfo
    {
        [DataMember(Name = "username")] public string Username { get; set; }
        [DataMember(Name = "email")] public string Email { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "country_code")] public string CountryCode { get; set; }
        [DataMember(Name = "language_code")] public string LanguageCode { get; set; }
        /*
        TODO:
	    CustomerGroupCode string      `json:"customer_group_code"`
	    BrandCode         string      `json:"brand_code"`
	    AccountType       string      `json:"account_type"`
	    SubscriptionType  string      `json:"subscription_type"`
	    Usage             int64       `json:"usage"`
	    Quota             int64       `json:"quota"`
	    BusinessUsage     int64       `json:"business_usage"`
	    BusinessQuota     int64       `json:"business_quota"`
	    WriteLocked       bool        `json:"write_locked"`
	    ReadLocked        bool        `json:"read_locked"`
	    LockedCause       interface{} `json:"locked_cause"`
	    WebHash           string      `json:"web_hash"`
	    AndroidHash       string      `json:"android_hash"`
	    IOSHash           string      `json:"ios_hash"`
        */
    }

    public class JottacloudOAuth
    {
        private const string CLIENT_ID = "jottacli";
        private const string SCOPE = "offline_access+openid";

        private HttpWebRequest CreateRequest(HttpMethod method, Uri uri, ICollection<KeyValuePair<string, string>> headers = null)
        {
            // Create a HTTP request for url
            var request = (HttpWebRequest)WebRequest.CreateHttp(uri);
            request.Method = method.ToString();

            request.ContentType = "application/x-www-form-urlencoded"; // Required (when there is data?)
            //request.Accept = "application/json"; // Optional
            request.KeepAlive = false; // Optional
            request.UserAgent = JaFS.UserAgentString;
            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    request.Headers.Add(kv.Key, kv.Value);
                }
            }
            //request.Timeout = TODO?
            return request;
        }
        public DataObjectType Request<DataObjectType>(string url, HttpMethod method, ICollection<KeyValuePair<string, string>> headers = null, ICollection<KeyValuePair<string, string>> data = null)
        {
            // HTTP Post form data (string)
            var uri = new Uri(url);
            var request = CreateRequest(method, uri, headers);
            if (data != null)
            {
                StringBuilder postData = new StringBuilder();
                foreach (var kv in data)
                {
                    postData.Append(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&");
                }
                postData.Length--;
                byte[] byteArray = Encoding.UTF8.GetBytes(postData.ToString());
                //request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;
                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK) //if (response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    throw new JFSError("Unexpected response code: " + response.StatusDescription);
                }
                using (Stream responseStream = response.GetResponseStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(DataObjectType));
                    return (DataObjectType)serializer.ReadObject(responseStream);
                }
            }
        }

        private LoginToken ParseLoginToken(string loginTokenString)
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

        private OpenidConfiguration RequestOpenidConfiguration(LoginToken loginToken)
        {
            // OpenID Connect request for configuration.
            // Simple Get request without any authentication.
            return Request<OpenidConfiguration>(loginToken.WellKnownLink, HttpMethod.Get);
        }

        private TokenObject RequestNewToken(LoginToken loginToken, OpenidConfiguration openidConfiguration)
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
            return Request<TokenObject>(openidConfiguration.TokenEndpoint, HttpMethod.Post, data: data);
        }

        private TokenObject RequestTokenRefresh(TokenObject token)
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
            return Request<TokenObject>(url, HttpMethod.Post, data: data);
        }

        private UserInfo RequestUserInfo(TokenObject token, OpenidConfiguration openidConfiguration)
        {
            // OpenID Connect request for information about authenticated end-user.
            var headers = new Dictionary<string, string>() { { "Authorization", "Bearer " + token.AccessToken } };
            return Request<UserInfo>(openidConfiguration.UserinfoEndpoint, HttpMethod.Get, headers: headers);
        }


        private CustomerInfo RequestCustomerInfo(TokenObject token)
        {
            // Jottacloud API request for information about authenticated end-user.
            // NOTE: This is not OpenID Connect, unrelated to OAuth, but uses a bit different API
            // than the file access in JaFS and the returned information is also somewhat overlapping
            // with the UserInfo request in OpenID Connect, so implemented here for now...
            var headers = new Dictionary<string, string>() { { "Authorization", "Bearer " + token.AccessToken } };
            return Request<CustomerInfo>("https://api.jottacloud.com/account/v1/customer", HttpMethod.Get, headers: headers);
        }

        public TokenObject CreateToken(string PersonalLoginToken)
        {
            // Create new token.
            // Input argument must be a Personal Login Token, base64 encoded json string,
            // generated interactively by user from Web GUI at https://www.jottacloud.com/web/secure.
            var loginToken = ParseLoginToken(PersonalLoginToken);
            var openidConfiguration = RequestOpenidConfiguration(loginToken);
            var token = RequestNewToken(loginToken, openidConfiguration);
            return token;
        }

        public TokenObject RefreshToken(TokenObject token)
        {
            // Refresh existing token.
            var newToken = RequestTokenRefresh(token);
            return newToken;
        }

        private static string ExportToken(TokenObject token)
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

        private static TokenObject ImportToken(string tokenString)
        {
            // Import token from JSON string, in the same format as the OpenID Connect API uses (lowercase member names with underscore).
            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(tokenString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(TokenObject));
                return (TokenObject)serializer.ReadObject(memoryStream);
            }
        }

        public CustomerInfo GetCustomerInfo(TokenObject token)
        {
            return RequestCustomerInfo(token);
        }
    }
}