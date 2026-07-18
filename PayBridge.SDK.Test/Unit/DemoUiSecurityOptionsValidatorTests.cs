using FluentAssertions;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class DemoUiSecurityOptionsValidatorTests
{
    [Fact]
    public void Validate_rejects_live_mode_when_not_explicitly_enabled()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();
        options.Mode = DemoRuntimeMode.Live;
        options.AllowLiveMode = false;

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(item => item.Contains("Mode cannot be Live"));
    }

    [Fact]
    public void Validate_rejects_missing_api_key_when_hosted_access_is_required()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();
        options.RequireAuthenticatedAccess = true;
        options.ApiKey = string.Empty;

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(item => item.Contains("ApiKey is required"));
    }

    [Fact]
    public void Validate_rejects_placeholder_api_key_when_hosted_access_is_required()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();
        options.RequireAuthenticatedAccess = true;
        options.ApiKey = "YOUR_DEMO_API_KEY";

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(item => item.Contains("ApiKey cannot use placeholder"));
    }

    [Fact]
    public void Validate_rejects_missing_csrf_values_when_csrf_protection_is_enabled()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();
        options.RequireCsrfHeader = true;
        options.CsrfHeaderName = string.Empty;
        options.CsrfHeaderValue = string.Empty;

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(item => item.Contains("CsrfHeaderName is required"));
        result.Failures.Should().Contain(item => item.Contains("CsrfHeaderValue is required"));
    }

    [Fact]
    public void Validate_rejects_placeholder_csrf_value_when_csrf_protection_is_enabled()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();
        options.RequireCsrfHeader = true;
        options.CsrfHeaderValue = "YOUR_DEMO_CSRF_TOKEN";

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(item => item.Contains("CsrfHeaderValue cannot use placeholder"));
    }

    [Fact]
    public void Validate_accepts_secure_sandbox_defaults()
    {
        var validator = new DemoUiSecurityOptionsValidator();
        var options = NewOptions();

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    private static DemoUiSecurityOptions NewOptions() => new()
    {
        RequireAuthenticatedAccess = true,
        ApiKey = "demo-api-key",
        Mode = DemoRuntimeMode.Sandbox,
        AllowLiveMode = false,
        RequireCsrfHeader = true,
        CsrfHeaderName = "X-Console-CSRF",
        CsrfHeaderValue = "csrf-token",
        RequestsPerMinute = 60
    };
}
