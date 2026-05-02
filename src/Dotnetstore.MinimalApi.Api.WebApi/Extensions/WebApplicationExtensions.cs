using System.Threading.RateLimiting;
using Asp.Versioning;
using Dotnetstore.MinimalApi.Api.WebApi.Configuration;
using Dotnetstore.MinimalApi.Api.WebApi.Endpoints;
using Dotnetstore.MinimalApi.Api.WebApi.Exceptions;
using Dotnetstore.MinimalApi.Api.WebApi.Filters;
using Dotnetstore.MinimalApi.Api.WebApi.Handlers;
using Dotnetstore.MinimalApi.Api.WebApi.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Scalar.AspNetCore;

namespace Dotnetstore.MinimalApi.Api.WebApi.Extensions;

internal static class WebApplicationExtensions
{
    extension(WebApplicationBuilder builder)
    {
        internal WebApplicationBuilder RegisterWebApi()
        {
            builder
                .AddServiceDefaults();
            
            builder.Services
                .AddSingleton<IValidateOptions<WebApiOptions>, WebApiOptionsValidator>()
                .AddOptions<WebApiOptions>()
                .Bind(builder.Configuration.GetSection(WebApiConfiguration.OptionsSectionName))
                .PostConfigure(options => options.ApplyDefaults())
                .ValidateOnStart();

            var webApiOptions = builder.ResolveWebApiOptions();
            
            builder.Services
                .AddSingleton<IValidateOptions<ApiKeySecurityOptions>, ApiKeySecurityOptionsValidator>()
                .AddOptions<ApiKeySecurityOptions>()
                .Bind(builder.Configuration.GetSection(ApiKeySecurityOptions.SectionName))
                .ValidateOnStart();

            builder.SetupHttpSecurity(webApiOptions);
            builder.SetupCors(webApiOptions);
            builder.SetupVersioning();
            builder.SetupRateLimiter(webApiOptions);
            builder.SetupAuthentication();

            builder.Services
                .AddOpenApi(options =>
                {
                    options.AddDocumentTransformer<SecurityDocumentTransformer>();
                })
                .AddProblemDetails()
                .AddSingleton<IWebApplicationHandlers, WebApplicationHandlers>()
                .AddSingleton<ITestEndpoints, TestEndpoints>()
                .AddSingleton<ISecureEndpoints, SecureEndpoints>()
                .AddExceptionHandler<DefaultExceptionHandler>()
                .AddScoped<ApiKeyFilter>();

            return builder;
        }

        private void SetupHttpSecurity(WebApiOptions webApiOptions)
        {
            if (webApiOptions.HttpsRedirection.Enabled)
            {
                builder.Services
                    .AddHttpsRedirection(options =>
                    {
                        options.RedirectStatusCode = webApiOptions.HttpsRedirection.RedirectStatusCode;
                        options.HttpsPort = webApiOptions.HttpsRedirection.HttpsPort;
                    });
            }
        
            if (webApiOptions.Hsts.Enabled)
            {
                builder.Services.AddHsts(options =>
                {
                    options.Preload = webApiOptions.Hsts.Preload;
                    options.IncludeSubDomains = webApiOptions.Hsts.IncludeSubDomains;
                    options.MaxAge = TimeSpan.FromDays(webApiOptions.Hsts.MaxAgeDays);
                });
            }
        }

        private void SetupCors(WebApiOptions webApiOptions)
        {
            builder.Services
                .AddCors(options =>
                {
                    options.AddPolicy(WebApiConfiguration.CorsPolicyName,
                        policy =>
                        {
                            policy
                                .WithOrigins(webApiOptions.Cors.AllowedOrigins!)
                                .WithMethods(webApiOptions.Cors.AllowedMethods!)
                                .AllowAnyHeader();
                        });
                });
        }
        
        private void SetupVersioning()
        {
            builder.Services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.ReportApiVersions = true;
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ApiVersionReader = new HeaderApiVersionReader(WebApiConfiguration.ApiVersionHeaderName);
                });
        }

        private void SetupRateLimiter(WebApiOptions webApiOptions)
        {            
            var rateLimitingOptions = webApiOptions.RateLimiting;

            builder.Services
                .AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = rateLimitingOptions.RejectionStatusCode;
                    options.OnRejected = async (context, token) =>
                    {
                        if (context.HttpContext.Response.HasStarted) return;
                        await context.HttpContext.Response.WriteAsync(rateLimitingOptions.RejectionMessage, token);
                    };
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetRateLimitPartitionKey(httpContext, rateLimitingOptions.PartitionKeyFallback),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                QueueLimit = rateLimitingOptions.GlobalQueueLimit,
                                PermitLimit = rateLimitingOptions.GlobalPermitLimit,
                                Window = TimeSpan.FromSeconds(rateLimitingOptions.GlobalWindowSeconds)
                            }));
                    options.AddPolicy(WebApiConfiguration.ShortRateLimitPolicyName, context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetRateLimitPartitionKey(context, rateLimitingOptions.PartitionKeyFallback),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                QueueLimit = rateLimitingOptions.ShortQueueLimit,
                                PermitLimit = rateLimitingOptions.ShortPermitLimit,
                                Window = TimeSpan.FromSeconds(rateLimitingOptions.ShortWindowSeconds)
                            }));
                });
        }

        private void SetupAuthentication()
        {
            builder.Services
                .AddSingleton<IValidateOptions<EntraIdOptions>, EntraIdOptionsValidator>()
                .AddOptions<EntraIdOptions>()
                .Bind(builder.Configuration.GetSection(EntraIdOptions.SectionName))
                .ValidateOnStart();

            builder.Services
                .AddAuthentication(WebApiConfiguration.EntraIdAuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    builder.Configuration,
                    configSectionName: EntraIdOptions.SectionName,
                    jwtBearerScheme: WebApiConfiguration.EntraIdAuthenticationScheme);

            builder.Services
                .AddAuthorizationBuilder()
                .AddPolicy(AuthorizationPolicies.CanReadTest, policy =>
                {
                    policy.AuthenticationSchemes = [WebApiConfiguration.EntraIdAuthenticationScheme];
                    policy.RequireAuthenticatedUser();
                    policy.RequireScope(Scopes.TestRead);
                });
        }

        private WebApiOptions ResolveWebApiOptions() =>
            (builder.Configuration
                .GetSection(WebApiConfiguration.OptionsSectionName)
                .Get<WebApiOptions>()
            ?? new WebApiOptions())
            .ApplyDefaults();

        private static string GetRateLimitPartitionKey(HttpContext httpContext, string partitionKeyFallback) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? partitionKeyFallback;
    }

    extension(WebApplication app)
    {
        internal WebApplication RegisterMiddlewares()
        {
            var webApiOptions = app.Services.GetRequiredService<IOptions<WebApiOptions>>().Value;

            if (app.Environment.IsDevelopment())
            {
                app
                    .MapOpenApi();

                var apiKeyOptions = app.Services.GetRequiredService<IOptions<ApiKeySecurityOptions>>().Value;
                var entraIdOptions = app.Services.GetRequiredService<IOptions<EntraIdOptions>>().Value;
                var apiScopeUri = $"api://{entraIdOptions.ClientId}/{Scopes.TestRead}";

                app.MapScalarApiReference("/docs", options =>
                {
                    options.WithTitle("Dotnetstore MinimalApi Web API");
                    options.AddPreferredSecuritySchemes(
                        WebApiConfiguration.OAuth2SecuritySchemeName,
                        WebApiConfiguration.ApiKeySecuritySchemeName);
                    options.AddApiKeyAuthentication(WebApiConfiguration.ApiKeySecuritySchemeName, apiKey =>
                    {
                        apiKey.Value = apiKeyOptions.Value;
                        apiKey.Name = apiKeyOptions.HeaderName;
                    });
                    options.AddOAuth2Authentication(WebApiConfiguration.OAuth2SecuritySchemeName, oauth =>
                    {
                        oauth.Flows = new ScalarFlows
                        {
                            AuthorizationCode = new AuthorizationCodeFlow
                            {
                                ClientId = entraIdOptions.ClientId,
                                Pkce = Pkce.Sha256,
                                SelectedScopes = [apiScopeUri]
                            }
                        };
                    });
                });
            }

            if (!app.Environment.IsDevelopment() && webApiOptions.Hsts.Enabled)
            {
                app.UseHsts();
            }

            if (webApiOptions.HttpsRedirection.Enabled)
            {
                app.UseHttpsRedirection();
            }

            app
                .MapDefaultEndpoints()
                .UseCors(WebApiConfiguration.CorsPolicyName)
                .UseRateLimiter()
                .UseExceptionHandler()
                .UseAuthentication()
                .UseAuthorization();
        
            return app;
        }

        internal async ValueTask RunWebApiAsync(CancellationToken cancellationToken = default)
        {
            await app.RunAsync(cancellationToken);
        }
    }
}