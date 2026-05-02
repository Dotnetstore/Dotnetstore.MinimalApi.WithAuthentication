using Microsoft.Extensions.Options;

namespace Dotnetstore.MinimalApi.Api.WebApi.Configuration;

internal sealed class EntraIdOptionsValidator : IValidateOptions<EntraIdOptions>
{
    public ValidateOptionsResult Validate(string? name, EntraIdOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Instance)
            || !Uri.TryCreate(options.Instance, UriKind.Absolute, out var instanceUri)
            || instanceUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("AzureAd:Instance must be a valid absolute https URL.");
        }

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            errors.Add("AzureAd:TenantId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            errors.Add("AzureAd:ClientId cannot be empty.");
        }

        if (options.ValidAudiences?.Any(string.IsNullOrWhiteSpace) == true)
        {
            errors.Add("AzureAd:ValidAudiences cannot contain empty values.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
