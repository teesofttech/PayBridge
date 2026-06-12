using System.Net;
using System.Text;

namespace PayBridge.SDK.Test.Helpers;

/// <summary>
/// A fake HttpMessageHandler that returns pre-queued responses without hitting the network.
/// Supports multi-step gateways (e.g. Monnify/Interswitch that do OAuth then pay).
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<MockHttpResponse> _queue = new();
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>All requests made through this handler, in order.</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    /// <summary>The most recent request made through this handler.</summary>
    public HttpRequestMessage? LastRequest => _requests.Count > 0 ? _requests[^1] : null;

    // ── Fluent setup ──────────────────────────────────────────────────────────

    /// <summary>Queue a single JSON response.</summary>
    public MockHttpMessageHandler RespondWith(
        HttpStatusCode statusCode,
        string jsonBody,
        string contentType = "application/json")
    {
        _queue.Enqueue(new MockHttpResponse(statusCode, jsonBody, contentType));
        return this;
    }

    /// <summary>Queue a plain-text / XML response.</summary>
    public MockHttpMessageHandler RespondWithText(
        HttpStatusCode statusCode,
        string body,
        string contentType = "text/plain")
    {
        _queue.Enqueue(new MockHttpResponse(statusCode, body, contentType));
        return this;
    }

    /// <summary>Queue an empty 200 OK response.</summary>
    public MockHttpMessageHandler RespondWithOk()
        => RespondWith(HttpStatusCode.OK, "{}");

    // ── Core override ─────────────────────────────────────────────────────────

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (_queue.Count == 0)
        {
            throw new InvalidOperationException(
                $"MockHttpMessageHandler: no queued response for {request.Method} {request.RequestUri}. " +
                "Did you forget to call RespondWith()?");
        }

        var mock = _queue.Dequeue();
        var response = new HttpResponseMessage(mock.StatusCode)
        {
            Content = new StringContent(mock.Body, Encoding.UTF8, mock.ContentType)
        };

        return Task.FromResult(response);
    }

    // ── Assertion helpers ─────────────────────────────────────────────────────

    /// <summary>Assert that the handler received exactly <paramref name="count"/> requests.</summary>
    public void AssertRequestCount(int count)
    {
        if (_requests.Count != count)
            throw new InvalidOperationException(
                $"Expected {count} HTTP request(s) but got {_requests.Count}.");
    }

    /// <summary>Assert the last request targeted the expected URL path.</summary>
    public void AssertLastRequestPath(string expectedPathContains)
    {
        var path = LastRequest?.RequestUri?.ToString() ?? "(none)";
        if (!path.Contains(expectedPathContains, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Expected last request URL to contain '{expectedPathContains}' but was '{path}'.");
    }

    /// <summary>Assert the last request used the expected HTTP method.</summary>
    public void AssertLastMethod(HttpMethod method)
    {
        if (LastRequest?.Method != method)
            throw new InvalidOperationException(
                $"Expected HTTP {method} but last request was {LastRequest?.Method}.");
    }

    /// <summary>
    /// Build an <see cref="HttpClient"/> wired to this handler.
    /// Optionally sets a base address.
    /// </summary>
    public HttpClient BuildClient(string? baseAddress = null)
    {
        var client = new HttpClient(this);
        if (baseAddress != null)
            client.BaseAddress = new Uri(baseAddress);
        return client;
    }
}

/// <summary>A single pre-configured HTTP response in the queue.</summary>
public sealed record MockHttpResponse(
    HttpStatusCode StatusCode,
    string Body,
    string ContentType);
