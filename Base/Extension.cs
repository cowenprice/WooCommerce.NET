using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using WooCommerce.NET.WooCommerce.Parameters;

namespace WooCommerceNET.Base
{
    public static class Extension
    {
        public static PropertyInfo FindByName(this IEnumerable<PropertyInfo> properties, string name)
        {
            return properties.ToList().Find(x => x.Name.ToLower() == name.ToLower());
        }

        public static Task<Stream> GetRequestStreamAsync(this HttpWebRequest request)
        {
            var tcs = new TaskCompletionSource<Stream>();

            try
            {
                request.BeginGetRequestStream(iar =>
                {
                    try
                    {
                        var response = request.EndGetRequestStream(iar);
                        tcs.SetResult(response);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                }, null);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
            }

            return tcs.Task;
        }

        public static Task<HttpWebResponse> GetResponseAsync(this HttpWebRequest request)
        {
            var tcs = new TaskCompletionSource<HttpWebResponse>();

            try
            {
                request.BeginGetResponse(iar =>
                {
                    try
                    {
                        var response = (HttpWebResponse)request.EndGetResponse(iar);
                        tcs.SetResult(response);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                }, null);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
            }

            return tcs.Task;
        }
    }

    /// <summary>
    /// Extensions for setting restricted Headers.
    /// More details: http://stackoverflow.com/questions/239725/cannot-set-some-http-headers-when-using-system-net-webrequest
    /// </summary>
    public static class HttpWebRequestExtensions
    {
        static string[] RestrictedHeaders = new string[] {
            "Accept",
            "Connection",
            "Content-Length",
            "Content-Type",
            "Date",
            "Expect",
            "Host",
            "If-Modified-Since",
            "Keep-Alive",
            "Proxy-Connection",
            "Range",
            "Referer",
            "Transfer-Encoding",
            "User-Agent"
    };

        static Dictionary<string, PropertyInfo> HeaderProperties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        static HttpWebRequestExtensions()
        {
            Type type = typeof(HttpWebRequest);
            foreach (string header in RestrictedHeaders)
            {
                string propertyName = header.Replace("-", "");
                PropertyInfo headerProperty = type.GetRuntimeProperty(propertyName);
                HeaderProperties[header] = headerProperty;
            }
        }

        public static void SetRawHeader(this HttpWebRequest request, string name, string value)
        {
            if (HeaderProperties.ContainsKey(name))
            {
                PropertyInfo property = HeaderProperties[name];
                if (property.PropertyType == typeof(DateTime))
                    property.SetValue(request, DateTime.Parse(value), null);
                else if (property.PropertyType == typeof(bool))
                    property.SetValue(request, bool.Parse(value), null);
                else if (property.PropertyType == typeof(long))
                    property.SetValue(request, long.Parse(value), null);
                else
                    property.SetValue(request, value, null);
            }
            else
            {
                request.Headers[name] = value;
            }
        }
        public static string ToParameterString(this IWCItemParameters paramterObj)
        {
            var parmBuilder = new StringBuilder();

            foreach (var property in paramterObj.GetType().GetProperties())
            {
                var atts = property.GetCustomAttributes(true).ToDictionary(a => a.GetType().Name, a => a);
                var propName = "";
                if (atts.TryGetValue("JsonPropertyName", out object value))
                    propName = value.ToString();
                //WC Uses Snake Case for request params, so unless specified in the parameterObj, convert to snake case
                else propName = propName.ToSnakeCase();

                //If it's some form of list, we need add each item in the 
                if (property.IsGenericList())
                    foreach (var item in property.GetValue(paramterObj) as IList)
                        parmBuilder.Append($"{propName}[]={item.ToString()}&");
                
                else
                    parmBuilder.Append($"{propName}={property.GetValue(paramterObj)}&");
                
            }
            return parmBuilder.ToString().TrimEnd('&');
        }
        public static bool IsGenericList(this object o)
        {
            var oType = o.GetType();
            return oType.GetTypeInfo().IsGenericType && oType.GetGenericTypeDefinition() == typeof(IList<>);
        }
    }
}
