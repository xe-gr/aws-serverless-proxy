using System.Collections.Generic;

namespace ServerlessProxy
{
    public class AppSettings
    {
        public string LambdaFunction { get; set; }
        public bool InvokeIfLambdaFunctionFails { get; set; }
        public string ProxyUrl { get; set; }
        public List<string> DeclinePaths { get; set; }
        public string RequirePathStart { get; set; }
    }
}