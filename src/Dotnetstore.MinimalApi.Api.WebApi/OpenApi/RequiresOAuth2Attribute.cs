namespace Dotnetstore.MinimalApi.Api.WebApi.OpenApi;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class RequiresOAuth2Attribute(params string[] scopes) : Attribute
{
    public string[] Scopes { get; } = scopes;
}
