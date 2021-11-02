using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using HttpContextMoq;
using HttpContextMoq.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json;
using ServerlessProxy.Models;
using Xunit;

namespace ServerlessProxy.Tests
{
    public class ProxyInvokerTests
    {
        private const string ConfiguredProxyUrl = "http://nohost.com";

        [Fact]
        public void DeclineDueToDeclinedSpecificPath()
        {
            var context = new HttpContextMock().SetupUrl("http://nohost.com/invalid");

            var proxy = new ProxyInvoker();
            var result = proxy.Invoke(null, new AppSettings { DeclinePaths = new List<string> { "/invalid" } },
                null, GetLogger(), context, null);

            Assert.Empty(result);

            context.Mock.Verify(x => x.Abort(), Times.Exactly(1));
        }

        [Fact]
        public void DeclineDueToNotValidStartPath()
        {
            var context = new HttpContextMock().SetupUrl("http://nohost.com/start");

            var proxy = new ProxyInvoker();
            var result = proxy.Invoke(null, new AppSettings { RequirePathStart = "/api" },
                null, GetLogger(), context, null);

            Assert.Empty(result);

            context.Mock.Verify(x => x.Abort(), Times.Exactly(1));
        }

        [Fact]
        public void LambdaFunctionFails()
        {
            var context = new HttpContextMock()
                .SetupUrl("http://nohost.com/api")
                .SetupRequestHeaders(new Dictionary<string, StringValues>());

            var settings = new AppSettings
                { InvokeIfLambdaFunctionFails = false, ProxyUrl = ConfiguredProxyUrl, LambdaFunction = "fake" };

            var lambda = new Mock<IAmazonLambda>(MockBehavior.Loose);
            lambda.Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(() => throw new InvalidOperationException()).Verifiable();

            var clientCreator = new Mock<ILambdaClientCreator>(MockBehavior.Strict);
            clientCreator.Setup(x => x.CreateClient(It.IsAny<AWSOptions>())).Returns(lambda.Object).Verifiable();

            var proxy = new ProxyInvoker();

            Assert.Throws<InvalidOperationException>(() =>
                proxy.Invoke(new AWSOptions(), settings, clientCreator.Object, GetLogger(), context, null));

            lambda.Verify(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            clientCreator.Verify(x => x.CreateClient(It.IsAny<AWSOptions>()), Times.Exactly(1));
        }

        [Fact]
        public void LambdaFunctionFailsButInvocationProceeds()
        {
            var context = new HttpContextMock()
                .SetupUrl("http://nohost.com/api")
                .SetupRequestHeaders(new Dictionary<string, StringValues>());

            var settings = new AppSettings
                { InvokeIfLambdaFunctionFails = true, ProxyUrl = ConfiguredProxyUrl, LambdaFunction = "fake" };

            var lambda = new Mock<IAmazonLambda>(MockBehavior.Loose);
            lambda.Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(() => throw new InvalidOperationException()).Verifiable();

            var clientCreator = new Mock<ILambdaClientCreator>(MockBehavior.Strict);
            clientCreator.Setup(x => x.CreateClient(It.IsAny<AWSOptions>())).Returns(lambda.Object).Verifiable();

            var proxy = new ProxyInvoker();
            var result = proxy.Invoke(new AWSOptions(), settings, clientCreator.Object, GetLogger(), context, null);

            Assert.Equal(settings.ProxyUrl, result);

            lambda.Verify(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            clientCreator.Verify(x => x.CreateClient(It.IsAny<AWSOptions>()), Times.Exactly(1));
            context.Mock.Verify(x => x.Abort(), Times.Exactly(0));
        }

        [Fact]
        public void LambdaFunctionDeclines()
        {
            LambdaFunctionCall(400, string.Empty, 1);
        }

        [Fact]
        public void LambdaFunctionAccepts()
        {
            LambdaFunctionCall(200, ConfiguredProxyUrl, 0);
        }

        private void LambdaFunctionCall(int statusCode, string expected, int abortCalls)
        {
            var context = new HttpContextMock()
                .SetupUrl("http://nohost.com/api")
                .SetupRequestHeaders(new Dictionary<string, StringValues>());

            var settings = new AppSettings
                { InvokeIfLambdaFunctionFails = true, ProxyUrl = "http://nohost.com", LambdaFunction = "fake" };

            var lambda = new Mock<IAmazonLambda>(MockBehavior.Loose);
            var lambdaResponse = new LambdaResponse { StatusCode = statusCode };
            var rsp = new InvokeResponse
            {
                Payload = new MemoryStream(System.Text.Encoding.Default.GetBytes(JsonConvert.SerializeObject(lambdaResponse)))
            };
            lambda.Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(rsp)).Verifiable();

            var clientCreator = new Mock<ILambdaClientCreator>(MockBehavior.Strict);
            clientCreator.Setup(x => x.CreateClient(It.IsAny<AWSOptions>())).Returns(lambda.Object).Verifiable();

            var proxy = new ProxyInvoker();
            var result = proxy.Invoke(new AWSOptions(), settings, clientCreator.Object, GetLogger(), context, null);

            Assert.Equal(expected, result);

            lambda.Verify(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1));
            clientCreator.Verify(x => x.CreateClient(It.IsAny<AWSOptions>()), Times.Exactly(1));
            context.Mock.Verify(x => x.Abort(), Times.Exactly(abortCalls));
        }

        private ILogger<ProxyInvoker> GetLogger()
        {
            return new Mock<ILogger<ProxyInvoker>>().Object;
        }
    }
}
