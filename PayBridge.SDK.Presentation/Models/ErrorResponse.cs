namespace PayBridge.SDK.Presentation.Models;

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Machine-readable error code
    /// </summary>
    public string ErrorCode { get; set; }
}
