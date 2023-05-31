using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using WooCommerceNET;

namespace WooCommerce.NET
{
    public partial class HttpMethodExt
    {
        public static System.Net.Http.HttpMethod Parse(string methodString)
        {
            methodString = methodString.ToLower();
            switch (methodString)
            {
                case "delete": return System.Net.Http.HttpMethod.Delete;
                case "get": return System.Net.Http.HttpMethod.Get;
                case "head":return System.Net.Http.HttpMethod.Head;
                case "options": return System.Net.Http.HttpMethod.Options;
                case "patch": return System.Net.Http.HttpMethod.Patch;
                case "post": return System.Net.Http.HttpMethod.Post;
                case "put": return System.Net.Http.HttpMethod.Put;
                case "trace": return System.Net.Http.HttpMethod.Trace;
                default: throw new HttpRequestException($"Invalid method requested: {methodString}");
            }
        }
        public static System.Net.Http.HttpMethod Parse(RequestMethod method)
        {
            switch (method)
            {
                case RequestMethod.DELETE: return System.Net.Http.HttpMethod.Delete;
                case RequestMethod.GET: return System.Net.Http.HttpMethod.Get;
                case RequestMethod.HEAD: return System.Net.Http.HttpMethod.Head;
                case RequestMethod.PATCH: return System.Net.Http.HttpMethod.Patch;
                case RequestMethod.POST: return System.Net.Http.HttpMethod.Post;
                case RequestMethod.PUT: return System.Net.Http.HttpMethod.Put;
                default: throw new HttpRequestException($"Invalid method requested");
            }
        }
    }
}
