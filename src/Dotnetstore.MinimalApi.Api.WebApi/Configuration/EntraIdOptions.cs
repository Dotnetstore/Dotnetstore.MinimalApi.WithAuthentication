namespace Dotnetstore.MinimalApi.Api.WebApi.Configuration;

internal sealed class EntraIdOptions
{
    internal const string SectionName = "AzureAd";

    public string Instance { get; init; } = "https://login.microsoftonline.com/";

    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string[]? ValidAudiences { get; init; }
}
