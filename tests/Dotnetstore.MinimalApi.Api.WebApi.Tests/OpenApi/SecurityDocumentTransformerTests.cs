using Dotnetstore.MinimalApi.Api.WebApi.Configuration;
using Dotnetstore.MinimalApi.Api.WebApi.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Shouldly;

namespace Dotnetstore.MinimalApi.Api.WebApi.Tests.OpenApi;

public sealed class SecurityDocumentTransformerTests
{
    private const string HeaderName = "X-TEST-API-KEY";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string ClientId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task TransformAsync_ShouldRegisterApiKeyAndOAuth2SecuritySchemes()
    {
        // Arrange
        var sut = CreateSut();
        var document = new OpenApiDocument { Components = null };

        // Act
        await sut.TransformAsync(document, null!, TestContext.Current.CancellationToken);

        // Assert
        var components = document.Components.ShouldNotBeNull();
        var schemes = components.SecuritySchemes.ShouldNotBeNull();

        var apiKey = schemes[WebApiConfiguration.ApiKeySecuritySchemeName].ShouldBeOfType<OpenApiSecurityScheme>();
        apiKey.Type.ShouldBe(SecuritySchemeType.ApiKey);
        apiKey.Name.ShouldBe(HeaderName);
        apiKey.In.ShouldBe(ParameterLocation.Header);

        var oauth = schemes[WebApiConfiguration.OAuth2SecuritySchemeName].ShouldBeOfType<OpenApiSecurityScheme>();
        oauth.Type.ShouldBe(SecuritySchemeType.OAuth2);
        var flow = oauth.Flows.ShouldNotBeNull().AuthorizationCode.ShouldNotBeNull();
        flow.AuthorizationUrl.ShouldNotBeNull().ToString()
            .ShouldBe($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize");
        flow.TokenUrl.ShouldNotBeNull().ToString()
            .ShouldBe($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token");
        flow.Scopes.ShouldNotBeNull().ShouldContainKey($"api://{ClientId}/{Scopes.TestRead}");
    }

    [Fact]
    public async Task TransformAsync_ShouldAssignApiKeyRequirement_ToOperationsWithoutOAuth2Metadata()
    {
        // Arrange
        var sut = CreateSut();
        var document = new OpenApiDocument();
        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(HttpMethod.Get, new OpenApiOperation());
        pathItem.AddOperation(HttpMethod.Post, new OpenApiOperation());
        document.Paths.Add("/test", pathItem);

        // Act
        await sut.TransformAsync(document, null!, TestContext.Current.CancellationToken);

        // Assert
        var operations = document.Paths["/test"].Operations.ShouldNotBeNull();
        operations.Count.ShouldBe(2);

        foreach (var operation in operations.Values)
        {
            var requirement = operation.Security.ShouldNotBeNull().ShouldHaveSingleItem();
            var schemeRef = requirement.Keys.Single().ShouldBeOfType<OpenApiSecuritySchemeReference>();
            schemeRef.Reference.Id.ShouldBe(WebApiConfiguration.ApiKeySecuritySchemeName);
        }
    }

    [Fact]
    public async Task TransformAsync_ShouldHandleNullContext_AndPathsWithoutOperations()
    {
        // Arrange
        var sut = CreateSut();
        var document = new OpenApiDocument();
        document.Paths.Add("/empty", new OpenApiPathItem());

        // Act
        var exception = await Record.ExceptionAsync(() => sut.TransformAsync(document, null!, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeNull();
        document.Paths["/empty"].Operations.ShouldBeNull();
    }

    private static SecurityDocumentTransformer CreateSut() =>
        new(
            Options.Create(new ApiKeySecurityOptions
            {
                HeaderName = HeaderName,
                Value = "unused-for-transformer-tests"
            }),
            Options.Create(new EntraIdOptions
            {
                Instance = "https://login.microsoftonline.com/",
                TenantId = TenantId,
                ClientId = ClientId
            }));
}
