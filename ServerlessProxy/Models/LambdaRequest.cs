using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ServerlessProxy.Models
{
    public class LambdaRequest
    {
        public Dictionary<string, StringValues> Dictionary { get; set; } = new Dictionary<string, StringValues>();
        public PathString Path { get; set; }
        public QueryString Query { get; set; }
        public string Method { get; set; }
        public string Host { get; set; }
        public bool IsHttps { get; set; }
    }
}
