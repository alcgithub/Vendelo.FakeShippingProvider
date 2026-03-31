using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vendelo.FakeShippingProvider.Middleware;
using Vendelo.FakeShippingProvider.Options;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            services.AddSingleton<AppOptions>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var opt = new AppOptions();
                cfg.Bind(opt);

                if (string.IsNullOrWhiteSpace(opt.Auth.Mode))
                    opt.Auth.Mode = "both";
                if (string.IsNullOrWhiteSpace(opt.Auth.StaticToken))
                    opt.Auth.StaticToken = "vendelo-static-token";
                if (string.IsNullOrWhiteSpace(opt.Auth.OAuthClientId))
                    opt.Auth.OAuthClientId = "vendelo-client";
                if (string.IsNullOrWhiteSpace(opt.Auth.OAuthClientSecret))
                    opt.Auth.OAuthClientSecret = "vendelo-secret";
                if (string.IsNullOrWhiteSpace(opt.Auth.OAuthRefreshToken))
                    opt.Auth.OAuthRefreshToken = "vendelo-oauth-refresh-token";
                if (string.IsNullOrWhiteSpace(opt.Auth.OAuthAccessToken))
                    opt.Auth.OAuthAccessToken = "vendelo-oauth-access-token";
                if (opt.Auth.OAuthExpiresIn <= 0)
                    opt.Auth.OAuthExpiresIn = 3600;
                if (string.IsNullOrWhiteSpace(opt.Auth.OAuthTokenType))
                    opt.Auth.OAuthTokenType = "Bearer";
                if (string.IsNullOrWhiteSpace(opt.Storage.DataFile))
                    opt.Storage.DataFile = "data/db.json";

                return opt;
            });

            services.AddSingleton<IDataStore, JsonDataStore>();
            services.AddSingleton<IAuthService, AuthService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseMiddleware<RequestAuditMiddleware>();
            app.UseMiddleware<BearerAuthMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

