using System;
using System.Collections.Generic;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServerlessProxy.Models;

namespace ServerlessProxy
{
    public class ProxyInvoker
    {
        public string Invoke(AWSOptions options,
            AppSettings settings,
            ILambdaClientCreator clientCreator,
            ILogger logger,
            HttpContext context, IDictionary<string, object> args)
        {
            logger.Log(LogLevel.Debug, $"Incoming request for path {context.Request.Path.Value}");

            if (DeclineSpecificPath(settings, context.Request.Path.Value) ||
                DeclineRequiredPath(settings, context.Request.Path.Value))
            {
                logger.Log(LogLevel.Information,
                    $"Incoming request for path {context.Request.Path.Value} declined due to configuration");

                context.Abort();
                return string.Empty;
            }

            using (var lambdaClient = clientCreator.CreateClient(options))
            {
                logger.Log(LogLevel.Debug, "Preparing request to lambda function");

                var lambdaRequest = new LambdaRequest();
                foreach (var header in context.Request.Headers)
                {
                    lambdaRequest.Dictionary.Add(header.Key, header.Value);
                }

                lambdaRequest.Path = context.Request.Path;
                lambdaRequest.Query = context.Request.QueryString;
                lambdaRequest.IsHttps = context.Request.IsHttps;
                lambdaRequest.Method = context.Request.Method;
                lambdaRequest.Host = context.Request.Host.Value;

                var json = JsonConvert.SerializeObject(lambdaRequest);

                var invocation = new InvokeRequest
                {
                    FunctionName = settings.LambdaFunction,
                    InvocationType = InvocationType.RequestResponse,
                    Payload = json
                };

                InvokeResponse lambdaResponse;
                try
                {
                    logger.Log(LogLevel.Trace, "Invoking lambda function");
                    lambdaResponse = lambdaClient.InvokeAsync(invocation).Result;
                    logger.Log(LogLevel.Trace, "Invocation complete");
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, $"Invocation failed\r\n{ex}");

                    if (settings.InvokeIfLambdaFunctionFails)
                    {
                        logger.Log(LogLevel.Warning, "Passing to backend service despite failure");

                        return settings.ProxyUrl;
                    }

                    throw;
                }

                var b = new byte[lambdaResponse.Payload.Length];
                lambdaResponse.Payload.Read(b, 0, (int)lambdaResponse.Payload.Length);
                var r = JsonConvert.DeserializeObject<LambdaResponse>(System.Text.Encoding.Default
                    .GetString(b));

                if (r?.StatusCode == 200)
                {
                    logger.Log(LogLevel.Debug, "Passing to backend service");

                    return settings.ProxyUrl;
                }

                logger.Log(LogLevel.Error, $"Lambda responded with {r?.StatusCode}, dropping call");

                context.Abort();
                return string.Empty;
            }
        }

        private bool DeclineSpecificPath(AppSettings settings, string pathValue)
        {
            if (settings.DeclinePaths == null || settings.DeclinePaths.Count == 0)
            {
                return false;
            }

            return settings.DeclinePaths.Contains(pathValue);
        }

        private bool DeclineRequiredPath(AppSettings settings, string pathValue)
        {
            if (string.IsNullOrEmpty(settings.RequirePathStart))
            {
                return false;
            }

            return !pathValue.StartsWith(settings.RequirePathStart);
        }
    }
}