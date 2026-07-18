using Microsoft.Extensions.Options;
using PayBridge.SDK.Example.Models;

namespace PayBridge.SDK.Example.Services;

public sealed class DemoUiSecurityOptionsValidator : IValidateOptions<DemoUiSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, DemoUiSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (options.RequestsPerMinute < 1)
        {
            failures.Add("DemoUiSecurity:RequestsPerMinute must be greater than zero.");
        }

        if (options.Mode == DemoRuntimeMode.Live && !options.AllowLiveMode)
        {
            failures.Add("DemoUiSecurity:Mode cannot be Live unless DemoUiSecurity:AllowLiveMode is true.");
        }

        if (options.RequireAuthenticatedAccess && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("DemoUiSecurity:ApiKey is required when DemoUiSecurity:RequireAuthenticatedAccess is true.");
        }
        else if (options.RequireAuthenticatedAccess && IsPlaceholderSecret(options.ApiKey))
        {
            failures.Add("DemoUiSecurity:ApiKey cannot use placeholder values in hosted mode.");
        }

        if (options.RequireCsrfHeader)
        {
            if (string.IsNullOrWhiteSpace(options.CsrfHeaderName))
            {
                failures.Add("DemoUiSecurity:CsrfHeaderName is required when DemoUiSecurity:RequireCsrfHeader is true.");
            }

            if (string.IsNullOrWhiteSpace(options.CsrfHeaderValue))
            {
                failures.Add("DemoUiSecurity:CsrfHeaderValue is required when DemoUiSecurity:RequireCsrfHeader is true.");
            }
            else if (IsPlaceholderSecret(options.CsrfHeaderValue))
            {
                failures.Add("DemoUiSecurity:CsrfHeaderValue cannot use placeholder values when CSRF protection is enabled.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsPlaceholderSecret(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("REPLACE_ME", StringComparison.OrdinalIgnoreCase);
    }
}
