namespace PayBridge.SDK.Example.Models;

public enum DemoRuntimeMode
{
    Mock = 0,
    Sandbox = 1,
    Live = 2
}

public sealed class DemoUiSecurityOptions
{
    public const string SectionName = "DemoUiSecurity";

    // Hosted environments must require authenticated access to demo APIs.
    public bool RequireAuthenticatedAccess { get; set; } = true;

    // Shared API key for non-local environments. Keep in secure stores only.
    public string ApiKey { get; set; } = string.Empty;

    // Live mode is blocked by default and can only be enabled server-side.
    public DemoRuntimeMode Mode { get; set; } = DemoRuntimeMode.Sandbox;

    public bool AllowLiveMode { get; set; } = false;

    // Basic anti-forgery guard for state-changing browser requests.
    public bool RequireCsrfHeader { get; set; } = true;
    public string CsrfHeaderName { get; set; } = "X-Console-CSRF";
    public string CsrfHeaderValue { get; set; } = string.Empty;

    // Global API throttling.
    public int RequestsPerMinute { get; set; } = 60;
}
