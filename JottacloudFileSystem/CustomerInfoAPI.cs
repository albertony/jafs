using System.Collections.Generic;
using System.Runtime.Serialization;
using Jottacloud.OAuthData;

namespace Jottacloud
{

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

    public sealed class CustomerInfoAPI
    {
        public static CustomerInfo GetCustomerInfo(TokenObject token)
        {
            // Jottacloud API request for information about authenticated end-user.
            // NOTE: This is not OpenID Connect, unrelated to OAuth, but uses a bit different API
            // than the file access in JaFS and the returned information is also somewhat overlapping
            // with the UserInfo request in OpenID Connect, so implemented here for now...
            var headers = new Dictionary<string, string>() { { "Authorization", "Bearer " + token.AccessToken } };
            return JsonAPI.Request<CustomerInfo>("https://api.jottacloud.com/account/v1/customer", HttpMethod.Get, headers: headers);
        }
    }
}
