using Amazon.Extensions.NETCore.Setup;
using AspNetCore.Proxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ServerlessProxy
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; private set; }

        private AWSOptions  _options;
        private AppSettings _settings;
        private ILogger     _logger;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddProxies();
            services.AddControllers();

            _settings = Configuration.GetSection("AppSettings").Get<AppSettings>();
            _options = Configuration.GetAWSOptions();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            _logger = app.ApplicationServices.GetService<ILogger<ProxyInvoker>>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.RunProxy(proxy => proxy
                .UseHttp((context, args) =>
                    new ProxyInvoker().Invoke(_options, _settings, new LambdaClientCreator(), _logger, context, args)));

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
