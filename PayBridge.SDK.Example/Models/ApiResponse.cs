namespace PayBridge.SDK.Example.Models;

/// <summary>
/// Generic API envelope returned by every endpoint in this example.
/// </summary>
/// <typeparam name="T">The payload type.</typeparam>
public class ApiResponse<T>
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The operation result (null on failure).</summary>
    public T? Data { get; init; }

    /// <summary>Machine-readable error code (null on success).</summary>
    public string? ErrorCode { get; init; }

    // ── Factories ────────────────────────────────────────────────────────────

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, string errorCode = "ERROR") =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}
