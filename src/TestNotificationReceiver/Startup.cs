using System;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Eshopworld.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TestNotificationReceiver
{
    public class Startup
    {
        private readonly TelemetrySettings _telemetrySettings = new TelemetrySettings();
        private readonly IBigBrother _bb;
        private readonly IConfigurationRoot _configuration;

        private const string STSPolicyName = "NotificationChannelScope";
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="env">hosting environment</param>
        public Startup(IHostingEnvironment env)
        {
            _configuration = EswDevOpsSdk.BuildConfiguration(env.ContentRootPath, env.EnvironmentName);
            var internalKey = _configuration["BBInstrumentationKey"];
            if (string.IsNullOrEmpty(internalKey))
            {
                throw new ApplicationException($"BBIntrumentationKey not found for environment {env.EnvironmentName}");
            }

            _bb = new BigBrother(internalKey, internalKey);
            _bb.UseKusto("eswtest", "westeurope", "tooling-ci", "3e14278f-8366-4dfd-bcc8-7e4e9d57f2c1");
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            try
            {


                services.AddAuthorization(options =>
                {
                    options.AddPolicy(STSPolicyName, policyBuilder => policyBuilder.RequireAssertion((context => true )));
                    //options.AddPolicy(STSPolicyName, policy =>
                    //    policy.RequireClaim("scope", "bullfrog.api.all")); //TODO: define real claim
                });

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test Scheme"; // has to match scheme in TestAuthenticationExtensions
                    options.DefaultChallengeScheme = "Test Scheme";
                }).AddTestAuth(options => { });

                //services
                    //.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddPolicyScheme("Bearer", "Bearer", options => options.);
                //    .AddIdentityServerAuthentication(x =>
                //{
                //    x.ApiName = _configuration["STSConfig:ApiName"];
                //    x.Authority = _configuration["STSConfig:Authority"];
                //});

                services.AddMvc(options=> options.Filters.Add(new AllowAnonymousFilter())).SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

                var builder = new ContainerBuilder();
                builder.Populate(services);
                builder.RegisterInstance(_bb).As<IBigBrother>().SingleInstance();
                builder.RegisterInstance(new KustoForwardingObserver(_bb)).SingleInstance();

                // add additional services or modules into container here

                var container = builder.Build();
                return new AutofacServiceProvider(container);

            }
            catch (Exception e)
            {
                _bb.Publish(e.ToExceptionEvent());
                throw;
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseBigBrotherExceptionHandler();

            app.UseAuthentication();

            app.UseNotification(new NotificationChannelMiddlewareOptions() {AuthorizationPolicyName = STSPolicyName })
                .SubscribeNotification<KeyVaultChangedNotification, KustoForwardingObserver>(app, o=>o.ReceiveNotification);
            
 
            app.UseMvc();
        }

     
    }
    public class TestAuthenticationHandler : AuthenticationHandler<TestAuthenticationOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<TestAuthenticationOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authenticationTicket = new AuthenticationTicket(
                new ClaimsPrincipal(Options.Identity),
                new AuthenticationProperties(),
                "Test Scheme");

            return Task.FromResult(AuthenticateResult.Success(authenticationTicket));
        }
    }

    public static class TestAuthenticationExtensions
    {
        public static AuthenticationBuilder AddTestAuth(this AuthenticationBuilder builder, Action<TestAuthenticationOptions> configureOptions)
        {
            return builder.AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>("Test Scheme", "Test Auth", configureOptions);
        }
    }

    public class TestAuthenticationOptions : AuthenticationSchemeOptions
    {
        public virtual ClaimsIdentity Identity { get; } = new ClaimsIdentity(new Claim[]
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", Guid.NewGuid().ToString()),
        }, "test");
    }
}
