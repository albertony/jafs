//
// Data classes for OAuth.
//
using System.Runtime.Serialization;

namespace Jottacloud.OAuthData
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
}