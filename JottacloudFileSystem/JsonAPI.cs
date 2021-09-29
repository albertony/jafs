//
// Utility for generic JSON-based API.
//
// Jottacloud's main file system api, "jfs", wrapped in our JaFS class is XML-based,
// but there are some supplementary APIs that are JSON-based (api.jottacloud.com
// endpoint, and everything OAuth-related).
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Runtime.Serialization.Json;

namespace Jottacloud
{
    public sealed class JsonAPI
    {
        public static HttpWebRequest CreateRequest(HttpMethod method, Uri uri, ICollection<KeyValuePair<string, string>> headers = null)
        {
            // Create a HTTP request for url
            var request = WebRequest.CreateHttp(uri);
            request.Method = method.ToString();

            request.ContentType = "application/x-www-form-urlencoded"; // Required (when there is data?)
            //request.Accept = "application/json"; // Optional
            request.KeepAlive = false; // Optional
            request.UserAgent = API.UserAgentString;
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
        public static DataObjectType Request<DataObjectType>(string url, HttpMethod method, ICollection<KeyValuePair<string, string>> headers = null, ICollection<KeyValuePair<string, string>> data = null)
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
    }
}
