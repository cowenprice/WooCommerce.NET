﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using WooCommerceNET.Base;
using System.Net.Http;
using System.Net.Http.Headers;
using WooCommerce.NET.WooCommerce.Parameters;
using System.Dynamic;
using System.Collections;
using WooCommerce.NET;
using System.Net.Http.Json;

namespace WooCommerceNET
{
    public class RestAPI
    {
        protected string wc_url = string.Empty;
        protected string wc_key = "";
        protected string wc_secret = "";
        //private bool wc_Proxy = false;

        protected bool AuthorizedHeader { get; set; }

        protected Func<string, string> jsonSeFilter;
        protected Func<string, string> jsonDeseFilter;
        protected Action<HttpWebRequest> webRequestFilter;
        protected Action<HttpRequestMessage> requestMessageFilter;
        protected Action<HttpWebResponse> webResponseFilter;
        protected Action<HttpResponseMessage> responseMessageFilter;

        /// <summary>
        /// For Wordpress REST API with OAuth 1.0 ONLY
        /// </summary>
        public string oauth_token { get; set; }

        /// <summary>
        /// For Wordpress REST API with OAuth 1.0 ONLY
        /// </summary>
        public string oauth_token_secret { get; set; }

        public WP_JWT_Object JWT_Object { get; set; }

        /// <summary>
        /// Authenticate Woocommerce API with JWT when set to True
        /// </summary>
        public bool WCAuthWithJWT { get; set; }

        /// <summary>
        /// Provide a function to modify the json string before deserilizing, this is for JWT Token ONLY!
        /// </summary>
        public Func<string, string> JWTDeserializeFilter { get; set; }

        /// <summary>
        /// Provide a function to modify the HttpWebRequest object, this is for JWT Token ONLY!
        /// </summary>
        public Action<HttpWebRequest> JWTRequestFilter { get; set; }
        public Action<HttpRequestMessage> JWTRequestFilterAlt { get; set; }
        /// <summary>
        /// If running in Debug mode, default is False.
        /// NOTE: Beware when setting Debug to True, as exceptions might contain sensetive information.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Initialize the RestAPI object
        /// </summary>
        /// <param name="url">
        /// WooCommerce REST API URL, e.g.: http://yourstore/wp-json/wc/v1/ 
        /// WordPress REST API URL, e.g.: http://yourstore/wp-json/
        /// </param>
        /// <param name="key">WooCommerce REST API Key Or WordPress consumerKey</param>
        /// <param name="secret">WooCommerce REST API Secret Or WordPress consumerSecret</param>
        /// <param name="authorizedHeader">WHEN using HTTPS, do you prefer to send the Credentials in HTTP HEADER?</param>
        /// <param name="jsonSerializeFilter">Provide a function to modify the json string after serilizing.</param>
        /// <param name="jsonDeserializeFilter">Provide a function to modify the json string before deserilizing.</param>
        /// <param name="requestFilter">Provide a function to modify the HttpWebRequest object.</param>
        /// <param name="responseFilter">Provide a function to grab information from the HttpWebResponse object.</param>
        public RestAPI(string url, string key, string secret, bool authorizedHeader = true,
                            Func<string, string> jsonSerializeFilter = null,
                            Func<string, string> jsonDeserializeFilter = null,
                            Action<HttpWebRequest> requestFilter = null,
                            Action<HttpWebResponse> responseFilter = null)//, bool useProxy = false)
        {
            if (string.IsNullOrEmpty(url))
                throw new Exception("Please use a valid WooCommerce Restful API url.");

            string urlLower = url.Trim().ToLower().TrimEnd('/');
            if (urlLower.EndsWith("wc-api/v1") || urlLower.EndsWith("wc-api/v2") || urlLower.EndsWith("wc-api/v3"))
                Version = APIVersion.Legacy;
            else if (urlLower.EndsWith("wp-json/wc/v1"))
                Version = APIVersion.Version1;
            else if (urlLower.EndsWith("wp-json/wc/v2"))
                Version = APIVersion.Version2;
            else if (urlLower.EndsWith("wp-json/wc/v3"))
                Version = APIVersion.Version3;
            else if (urlLower.Contains("wp-json/wc-"))
                Version = APIVersion.ThirdPartyPlugins;
            else if (urlLower.EndsWith("wp-json/wp/v2") || urlLower.EndsWith("wp-json"))
                Version = APIVersion.WordPressAPI;
            else if (urlLower.EndsWith("jwt-auth/v1/token"))
            {
                Version = APIVersion.WordPressAPIJWT;
                url = urlLower.Replace("jwt-auth/v1/token", "wp/v2");
            }
            else
            {
                Version = APIVersion.Unknown;
                throw new Exception("Unknown WooCommerce Restful API version.");
            }

            wc_url = url + (url.EndsWith("/") ? "" : "/");
            wc_key = key;
            AuthorizedHeader = authorizedHeader;

            //Why extra '&'? look here: https://wordpress.org/support/topic/woocommerce-rest-api-v3-problem-woocommerce_api_authentication_error/
            if ((url.ToLower().Contains("wc-api/v3") || !IsLegacy) && !wc_url.StartsWith("https", StringComparison.OrdinalIgnoreCase) && !(Version == APIVersion.WordPressAPI || Version == APIVersion.WordPressAPIJWT))
                wc_secret = secret + "&";
            else
                wc_secret = secret;

            jsonSeFilter = jsonSerializeFilter;
            jsonDeseFilter = jsonDeserializeFilter;
            webRequestFilter = requestFilter;
            webResponseFilter = responseFilter;

            //wc_Proxy = useProxy;
        }


        public bool IsLegacy
        {
            get
            {
                return Version == APIVersion.Legacy;
            }
        }

        public APIVersion Version { get; private set; }

        public string Url { get { return wc_url; } }

        /// <summary>
        /// Make Restful calls
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="method">HEAD, GET, POST, PUT, PATCH, DELETE</param>
        /// <param name="requestBody">If your call doesn't have a body, please pass string.Empty, not null.</param>
        /// <param name="parms"></param>
        /// <returns>json string</returns>
        public virtual async Task<string> SendHttpClientRequest<T>(string endpoint, RequestMethod method, T requestBody, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            HttpClient httpClient = new HttpClient();
            HttpRequestMessage httpRequest = null;
            HttpResponseMessage response = null;
            try
            {
                if (Version == APIVersion.WordPressAPI)
                {
                    if (string.IsNullOrEmpty(oauth_token) || string.IsNullOrEmpty(oauth_token_secret))
                        throw new Exception($"oauth_token and oauth_token_secret parameters are required when using WordPress REST API.");
                }

                if ((Version == APIVersion.WordPressAPIJWT || WCAuthWithJWT) && JWT_Object == null)
                {
                    httpRequest = new HttpRequestMessage(HttpMethod.Post, wc_url.Replace("wp/v2", "jwt-auth/v1/token")
                                                                                       .Replace("wc/v1", "jwt-auth/v1/token")
                                                                                       .Replace("wc/v2", "jwt-auth/v1/token")
                                                                                       .Replace("wc/v3", "jwt-auth/v1/token"));

                    httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>() { { "username", wc_key }, { "password", WebUtility.UrlEncode(wc_secret) } });
                    httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

                    if (JWTRequestFilter != null)
                        JWTRequestFilterAlt.Invoke(httpRequest);
                    response = await httpClient.SendAsync(httpRequest).ConfigureAwait(false);

                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (JWTDeserializeFilter != null)
                        result = JWTDeserializeFilter.Invoke(result);

                    JWT_Object = DeserializeJSon<WP_JWT_Object>(result);
                }

                if (wc_url.StartsWith("https", StringComparison.OrdinalIgnoreCase) && Version != APIVersion.WordPressAPI && Version != APIVersion.WordPressAPIJWT)
                {
                    if (AuthorizedHeader == false)
                    {
                        if (parms == null)
                            parms = new Dictionary<string, string>();

                        if (!parms.ContainsKey("consumer_key"))
                            parms.Add("consumer_key", wc_key);
                        if (!parms.ContainsKey("consumer_secret"))
                            parms.Add("consumer_secret", wc_secret);

                        //Expando version
                        if (parametersObj == null) parametersObj = new WCItemParameters();
                        parametersObj.ConsumerSecret = wc_secret;
                        parametersObj.ConsumerKey = wc_key;

                    }

                    //Allow accessing WordPress plugin REST API with WooCommerce secret and key.
                    //Url should be passed to RestAPI as WooCommerce Rest API url, e.g.: https://mystore.com/wp-json/wc/v3
                    //Endpoint should be starting with wp-json
                    if (endpoint.StartsWith("wp-json"))
                    {
                        httpRequest = new HttpRequestMessage(HttpMethodExt.Parse(method), new Uri(new Uri($"https://{new Uri(wc_url).Host}"), GetOAuthEndPoint(method.ToString(), endpoint, parms, parametersObj)));
                    }
                    else
                    {
                        httpRequest = new HttpRequestMessage(HttpMethodExt.Parse(method), wc_url + GetOAuthEndPoint(method.ToString(), endpoint, parms, parametersObj));
                    }

                    if (AuthorizedHeader == true)
                    {
                        if (WCAuthWithJWT && JWT_Object != null)
                        {
                            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JWT_Object.token);
                        }
                        else
                        { 
                            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(wc_key + ":" + wc_secret)));
                        }
                    }
                }
                else
                {
                    httpRequest = new HttpRequestMessage(HttpMethodExt.Parse(method), wc_url + GetOAuthEndPoint(method.ToString(), endpoint, parms, parametersObj));
                    if (Version == APIVersion.WordPressAPIJWT)
                    {
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JWT_Object.token);
                    }
                }

                if (requestMessageFilter != null)
                    requestMessageFilter.Invoke(httpRequest);

                if (requestBody != null && requestBody.GetType() != typeof(string))
                {
                    httpRequest.Content = JsonContent.Create(requestBody);

                }
                else
                {
                    if (requestBody != null && requestBody.ToString() != string.Empty)
                    {
                        if (requestBody.ToString() == "fileupload")
                        {
                            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(parms["path"]).ConfigureAwait(false));
                            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = parms["name"] };
                            httpRequest.Content = fileContent;

                        }
                        else
                        {
                            httpRequest.Content = JsonContent.Create(requestBody);
                        }
                    }
                }

                // asynchronously get a response
                response = await httpClient.SendAsync(httpRequest).ConfigureAwait(false);
                if (responseMessageFilter != null)
                    responseMessageFilter.Invoke(response);
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (HttpRequestException we)
            {
                if (httpRequest != null)
                    if (response != null)
                        throw new HttpRequestException(await response.Content.ReadAsStringAsync());
                    else
                        throw we;
                else
                    throw we;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public async Task<string> GetRestful(string endpoint, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            return await SendHttpClientRequest(endpoint.ToLower(), RequestMethod.GET, string.Empty, parms, parametersObj).ConfigureAwait(false);
        }

        public async Task<string> PostRestful(string endpoint, object jsonObject, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            return await SendHttpClientRequest(endpoint.ToLower(), RequestMethod.POST, jsonObject, parms, parametersObj).ConfigureAwait(false);
        }

        public async Task<string> PutRestful(string endpoint, object jsonObject, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            return await SendHttpClientRequest(endpoint.ToLower(), RequestMethod.PUT, jsonObject, parms, parametersObj).ConfigureAwait(false);
        }

        public async Task<string> DeleteRestful(string endpoint, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            return await SendHttpClientRequest(endpoint.ToLower(), RequestMethod.DELETE, string.Empty, parms, parametersObj).ConfigureAwait(false);
        }

        public async Task<string> DeleteRestful(string endpoint, object jsonObject, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            return await SendHttpClientRequest(endpoint.ToLower(), RequestMethod.DELETE, jsonObject, parms, parametersObj).ConfigureAwait(false);
        }

        protected string GetOAuthEndPoint(string method, string endpoint, Dictionary<string, string> parms = null, IWCItemParameters? parametersObj = null)
        {
            if (Version == APIVersion.WordPressAPIJWT || (wc_url.StartsWith("https", StringComparison.OrdinalIgnoreCase) && Version != APIVersion.WordPressAPI))
            {
                if (parms == null || parametersObj == null)
                    return endpoint;
                else
                {
                    string requestParms = string.Empty;
                    foreach (var parm in parms)
                        requestParms += parm.Key + "=" + parm.Value + "&";

                    return endpoint + "?" + requestParms.TrimEnd('&');
                }
            }

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("oauth_consumer_key", wc_key);

            if (Version == APIVersion.WordPressAPI)
                dic.Add("oauth_token", oauth_token);

            dic.Add("oauth_nonce", Guid.NewGuid().ToString("N"));
            dic.Add("oauth_signature_method", "HMAC-SHA256");
            dic.Add("oauth_timestamp", Common.GetUnixTime(false));
            dic.Add("oauth_version", "1.0");

            if (parms != null)
                foreach (var p in parms)
                    dic.Add(p.Key, p.Value);

            string base_request_uri = method.ToUpper() + "&" + Uri.EscapeDataString(wc_url + endpoint) + "&";
            string stringToSign = string.Empty;

            foreach (var parm in dic.OrderBy(x => x.Key))
                stringToSign += Uri.EscapeDataString(parm.Key) + "=" + Uri.EscapeDataString(parm.Value) + "&";

            base_request_uri = base_request_uri + Uri.EscapeDataString(stringToSign.TrimEnd('&'));

            if (Version == APIVersion.WordPressAPI)
                dic.Add("oauth_signature", Common.GetSHA256(wc_secret + "&" + oauth_token_secret, base_request_uri));
            else
                dic.Add("oauth_signature", Common.GetSHA256(wc_secret, base_request_uri));

            string parmstr = string.Empty;
            foreach (var parm in dic)
                parmstr += parm.Key + "=" + Uri.EscapeDataString(parm.Value) + "&";

            return endpoint + "?" + parmstr.TrimEnd('&');
        }

        protected async Task<string> GetStreamContent(Stream s, string charset)
        {
            StringBuilder sb = new StringBuilder();
            byte[] Buffer = new byte[512];
            int count = 0;
            count = await s.ReadAsync(Buffer, 0, Buffer.Length).ConfigureAwait(false);
            while (count > 0)
            {
                sb.Append(Encoding.GetEncoding(charset).GetString(Buffer, 0, count));
                count = await s.ReadAsync(Buffer, 0, Buffer.Length).ConfigureAwait(false);
            }

            return sb.ToString();
        }

        public virtual string SerializeJSon<T>(T t)
        {
            var serializerOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            string jsonString = JsonSerializer.Serialize(t, serializerOpts);

            if (t.GetType().GetMethod("FormatJsonS") != null)
            {
                jsonString = t.GetType().GetMethod("FormatJsonS").Invoke(null, new object[] { jsonString }).ToString();
            }

            if (IsLegacy)
                if (typeof(T).IsArray)
                    jsonString = "{\"" + typeof(T).Name.ToLower().Replace("[]", "s") + "\":" + jsonString + "}";
                else
                    jsonString = "{\"" + typeof(T).Name.ToLower() + "\":" + jsonString + "}";

            if (jsonSeFilter != null)
                jsonString = jsonSeFilter.Invoke(jsonString);

            return jsonString;
        }

        public virtual T DeserializeJSon<T>(string jsonString)
        {

            var serializerOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            if (jsonDeseFilter != null)
                jsonString = jsonDeseFilter.Invoke(jsonString);

            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, serializerOpts);
            }
            catch (Exception ex)
            {
                if (Debug)
                    throw new Exception(ex.Message + Environment.NewLine + Environment.NewLine + jsonString);
                else
                    throw ex;
            }
        }

        public string DateTimeFormat
        {
            get
            {
                return IsLegacy ? "yyyy-MM-ddTHH:mm:ssZ" : "yyyy-MM-ddTHH:mm:ssK";
            }
        }
    }

    public class WP_JWT_Object
    {
        public string token { get; set; }

        public string user_email { get; set; }

        public string user_nicename { get; set; }

        public string user_display_name { get; set; }
    }

    public enum RequestMethod
    {
        HEAD = 1,
        GET = 2,
        POST = 3,
        PUT = 4,
        PATCH = 5,
        DELETE = 6
    }

    public enum APIVersion
    {
        Unknown = 0,
        Legacy = 1,
        Version1 = 2,
        Version2 = 3,
        Version3 = 4,
        WordPressAPI = 90,
        WordPressAPIJWT = 91,
        ThirdPartyPlugins = 99
    }
}
