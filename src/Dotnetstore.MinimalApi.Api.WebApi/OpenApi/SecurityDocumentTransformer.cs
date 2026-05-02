using Dotnetstore.MinimalApi.Api.WebApi.Configuration;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Dotnetstore.MinimalApi.Api.WebApi.OpenApi;

internal sealed class SecurityDocumentTransformer(
    IOptions<ApiKeySecurityOptions> apiKeyOptions,
    IOptions<EntraIdOptions> entraIdOptions) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var entra = entraIdOptions.Value;
        var authBase = $"{entra.Instance.TrimEnd('/')}/{entra.TenantId}/oauth2/v2.0";
        var apiScopeUri = BuildScopeUri(entra.ClientId, Scopes.TestRead);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [WebApiConfiguration.ApiKeySecuritySchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = apiKeyOptions.Value.HeaderName,
                In = ParameterLocation.Header
            },
            [WebApiConfiguration.OAuth2SecuritySchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{authBase}/authorize"),
                        TokenUrl = new Uri($"{authBase}/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            [apiScopeUri] = "Read test data"
                        }
                    }
                }
            }
        };

        var oauthMetadataByPath = BuildOAuthMetadataLookup(context);

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null) continue;

            foreach (var (httpMethod, operation) in pathItem.Operations)
            {
                operation.Security ??= [];

                var key = (path, httpMethod.Method.ToUpperInvariant());

                if (oauthMetadataByPath.TryGetValue(key, out var oauth))
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(WebApiConfiguration.OAuth2SecuritySchemeName, document)]
                            = oauth.Scopes.Select(s => BuildScopeUri(entra.ClientId, s)).ToList()
                    });
                }
                else
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(WebApiConfiguration.ApiKeySecuritySchemeName, document)] = []
                    });
                }
            }
        }

        return Task.CompletedTask;
    }

    private static Dictionary<(string Path, string Method), RequiresOAuth2Attribute> BuildOAuthMetadataLookup(
        OpenApiDocumentTransformerContext? context)
    {
        var lookup = new Dictionary<(string Path, string Method), RequiresOAuth2Attribute>();

        if (context?.DescriptionGroups is null) return lookup;

        foreach (var description in context.DescriptionGroups.SelectMany(g => g.Items))
        {
            var oauth = description.ActionDescriptor.EndpointMetadata
                .OfType<RequiresOAuth2Attribute>()
                .FirstOrDefault();

            if (oauth is null || description.RelativePath is null || description.HttpMethod is null) continue;

            var path = "/" + description.RelativePath.TrimStart('/');
            lookup[(path, description.HttpMethod.ToUpperInvariant())] = oauth;
        }

        return lookup;
    }

    private static string BuildScopeUri(string clientId, string scope) =>
        $"api://{clientId}/{scope}";
}
