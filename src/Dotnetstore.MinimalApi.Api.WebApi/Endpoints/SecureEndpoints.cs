using System.Security.Claims;
using Dotnetstore.MinimalApi.Api.WebApi.Configuration;
using Dotnetstore.MinimalApi.Api.WebApi.Filters;
using Dotnetstore.MinimalApi.Api.WebApi.Handlers;
using Dotnetstore.MinimalApi.Api.WebApi.OpenApi;

namespace Dotnetstore.MinimalApi.Api.WebApi.Endpoints;

internal sealed class SecureEndpoints(
    IWebApplicationHandlers webApplicationHandlers) : ISecureEndpoints
{
    void ISecureEndpoints.MapEndpoints(WebApplication app)
    {
        app.MapGet("/secure/test", (ClaimsPrincipal user) =>
                Results.Ok(new { message = "Hello secure world!", user = user.Identity?.Name }))
            .WithApiVersionSet(webApplicationHandlers.GetApiVersionSet(app))
            .AddEndpointFilter<LogPerformanceFilter>()
            .RequireAuthorization(AuthorizationPolicies.CanReadTest)
            .WithMetadata(new RequiresOAuth2Attribute(Scopes.TestRead))
            .MapToApiVersion(1.0);
    }
}
