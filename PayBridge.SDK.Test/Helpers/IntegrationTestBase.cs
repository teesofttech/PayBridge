using Xunit;

namespace PayBridge.SDK.Test.Helpers;

/// <summary>
/// Base class for integration tests.
/// Any test class that derives from this will automatically be skipped
/// when required environment variables are missing, keeping CI output clean.
///
/// Usage:
/// <code>
/// public class PaystackIntegrationTests : IntegrationTestBase
/// {
///     public PaystackIntegrationTests()
///         : base("PAYSTACK_SECRET_KEY") { }
/// }
/// </code>
/// </summary>
public abstract class IntegrationTestBase
{
    private readonly string[] _requiredVars;

    /// <summary>
    /// The reason string reported by xUnit when a test is skipped.
    /// Set in the constructor if any env var is missing.
    /// </summary>
    protected string? SkipReason { get; }

    /// <summary>
    /// Whether all required env vars are present.
    /// Use this in test bodies to conditionally Skip:
    /// <code>Skip.If(ShouldSkip, SkipReason);</code>
    /// </summary>
    protected bool ShouldSkip => SkipReason != null;

    protected IntegrationTestBase(params string[] requiredEnvVars)
    {
        _requiredVars = requiredEnvVars;

        var missing = requiredEnvVars
            .Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)))
            .ToList();

        if (missing.Count > 0)
        {
            SkipReason = $"Integration test skipped — missing env var(s): {string.Join(", ", missing)}";
        }
    }

    /// <summary>
    /// Reads a required env var. Throws if missing (only call after checking ShouldSkip).
    /// </summary>
    protected string GetRequiredEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Required env var '{name}' is not set. Did you check ShouldSkip first?");
        return value;
    }

    /// <summary>
    /// Skips the current test if env vars are missing.
    /// Call this at the top of every integration test method.
    /// </summary>
    protected void SkipIfMissingEnvVars()
    {
        if (ShouldSkip)
            Skip.If(true, SkipReason!);
    }
}
