using System.Net;
using Dotnetstore.MinimalApi.Api.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Dotnetstore.MinimalApi.Api.WebApi.Tests.Endpoints;

/// <summary>
/// Covers <c>SecureEndpoints</c> and verifies the OIDC-protected route requires a valid token.
/// </summary>
public sealed class SecureEndpointsTests
{
    private const string SecurePath = "/secure/test";

    [Fact]
    public async Task SecureEndpoint_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        // Arrange
        await using var factory = new SecureEndpointsFactory();
        using var client = TestHttp.CreateClient(factory, TestHttp.HttpsLocalhost);
        using var request = TestHttp.CreateVersionedRequest(HttpMethod.Get, SecurePath, "1.0");

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SecureEndpoint_ShouldReturnUnauthorized_WhenApiKeyHeaderIsProvidedInsteadOfBearer()
    {
        // Arrange
        await using var factory = new SecureEndpointsFactory();
        using var client = TestHttp.CreateClient(factory, TestHttp.HttpsLocalhost);
        using var request = TestHttp.CreateAuthorizedVersionedRequest(HttpMethod.Get, SecurePath, "1.0");

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class SecureEndpointsFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                    ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222"
                });
            });
        }
    }
}
