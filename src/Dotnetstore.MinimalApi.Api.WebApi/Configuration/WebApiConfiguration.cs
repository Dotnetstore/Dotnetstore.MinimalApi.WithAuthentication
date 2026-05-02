namespace Dotnetstore.MinimalApi.Api.WebApi.Configuration;

internal static class WebApiConfiguration
{
    internal const string OptionsSectionName = "WebApi";
    internal const string ApiVersionHeaderName = "api-version";
    internal const string CorsPolicyName = "AllowDotnetstoreSpecificOrigins";
    internal const string ShortRateLimitPolicyName = "ShortLimit";
    internal const string EntraIdAuthenticationScheme = "EntraId";
    internal const string OAuth2SecuritySchemeName = "OAuth2";
    internal const string ApiKeySecuritySchemeName = "ApiKey";
}

