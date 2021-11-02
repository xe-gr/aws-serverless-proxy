using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda;

namespace ServerlessProxy
{
    public class LambdaClientCreator : ILambdaClientCreator
    {
        public IAmazonLambda CreateClient(AWSOptions options)
        {
            return options.CreateServiceClient<IAmazonLambda>();
        }
    }

    public interface ILambdaClientCreator
    {
        IAmazonLambda CreateClient(AWSOptions options);
    }
}
