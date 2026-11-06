namespace Ducky.Samples.Server.Client;

/// <summary>
/// An IHttpClientFactory handler that dispatches. In the browser its handler scope resolves the app's one store, so the
/// UI sees the action (INV-22); on the server it would get a different, empty store (SPEC §6.10), so only the WASM side
/// registers it.
/// </summary>
public sealed class DispatchingHandler(IDispatcher dispatcher) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(new ApiCallSent(request.RequestUri?.AbsolutePath ?? ""));
        return base.SendAsync(request, cancellationToken);
    }
}
