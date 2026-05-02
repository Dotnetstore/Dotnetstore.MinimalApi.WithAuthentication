using Dotnetstore.MinimalApi.Api.WebApi.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dotnetstore.MinimalApi.Api.WebApi.Tests.Configuration;

public sealed class EntraIdOptionsValidatorTests
{
    private const string ValidTenantId = "11111111-1111-1111-1111-111111111111";
    private const string ValidClientId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public void Validate_ShouldSucceed_WhenOptionsAreValid()
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = ValidTenantId,
            ClientId = ValidClientId
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Failed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-url")]
    [InlineData("http://login.microsoftonline.com/")]
    public void Validate_ShouldFail_WhenInstanceIsInvalid(string invalidInstance)
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = invalidInstance,
            TenantId = ValidTenantId,
            ClientId = ValidClientId
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);

        // Assert
        AssertValidationFailure(result, "AzureAd:Instance must be a valid absolute https URL.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_ShouldFail_WhenTenantIdIsEmpty(string invalidTenantId)
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = invalidTenantId,
            ClientId = ValidClientId
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);

        // Assert
        AssertValidationFailure(result, "AzureAd:TenantId cannot be empty.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_ShouldFail_WhenClientIdIsEmpty(string invalidClientId)
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = ValidTenantId,
            ClientId = invalidClientId
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);

        // Assert
        AssertValidationFailure(result, "AzureAd:ClientId cannot be empty.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenValidAudiencesContainsEmptyEntries()
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = ValidTenantId,
            ClientId = ValidClientId,
            ValidAudiences = ["valid-aud", " "]
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);

        // Assert
        AssertValidationFailure(result, "AzureAd:ValidAudiences cannot contain empty values.");
    }

    [Fact]
    public void Validate_ShouldReturnAllFailures_WhenAllOptionsAreInvalid()
    {
        // Arrange
        var sut = new EntraIdOptionsValidator();
        var options = new EntraIdOptions
        {
            Instance = "",
            TenantId = "",
            ClientId = ""
        };

        // Act
        var result = sut.Validate(Options.DefaultName, options);
        var failures = result.Failures.ShouldNotBeNull().ToList();

        // Assert
        result.Failed.ShouldBeTrue();
        failures.ShouldContain("AzureAd:Instance must be a valid absolute https URL.");
        failures.ShouldContain("AzureAd:TenantId cannot be empty.");
        failures.ShouldContain("AzureAd:ClientId cannot be empty.");
    }

    private static void AssertValidationFailure(ValidateOptionsResult result, string expectedError)
    {
        result.Failed.ShouldBeTrue();
        var failures = result.Failures.ShouldNotBeNull().ToList();
        failures.ShouldContain(expectedError);
    }
}
