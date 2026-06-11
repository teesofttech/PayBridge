namespace PayBridge.SDK.Test.Helpers;

/// <summary>
/// A minimal IHttpClientFactory that always returns an HttpClient
/// backed by the given MockHttpMessageHandler.
/// Needed for gateways that accept IHttpClientFactory in their constructor.
/// </summary>
public sealed class MockHttpClientFactory : IHttpClientFactory
{
    private readonly MockHttpMessageHandler _handler;

    public MockHttpClientFactory(MockHttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name = "")
        => _handler.BuildClient();
}
