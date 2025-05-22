using Microsoft.JSInterop;

namespace Ducky.Blazor;

/// <summary>
/// Abstract helper base for loading a JavaScript (ES6) module and calling its exports via Blazor's JSInterop.
/// </summary>
public abstract class JsModule : IAsyncDisposable
{
    private readonly AsyncLazy<IJSObjectReference> _jsModuleProvider;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    /// <summary>
    /// On construction, begins loading the JS module at <paramref name="moduleUrl"/>.
    /// </summary>
    /// <param name="js">Blazor JS runtime.</param>
    /// <param name="moduleUrl">JavaScript ES6 module URI.</param>
    protected JsModule(IJSRuntime js, string moduleUrl)
    {
        _jsModuleProvider = new AsyncLazy<IJSObjectReference>(async () =>
            await js
                .InvokeAsync<IJSObjectReference>("import", moduleUrl)
                .ConfigureAwait(false));
    }

    /// <summary>
    /// Calls a JS export with no expected return value.
    /// </summary>
    /// <param name="identifier">The JS export name.</param>
    /// <param name="args">Arguments for the export.</param>
    protected async ValueTask InvokeVoidAsync(string identifier, params object[]? args)
    {
        IJSObjectReference jsModule = await _jsModuleProvider.Value.ConfigureAwait(false);
        await jsModule
            .InvokeVoidAsync(identifier, _cancellationTokenSource.Token, args)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Calls a JS export and returns a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="identifier">The JS export name.</param>
    /// <param name="args">Arguments for the export.</param>
    /// <returns>The result returned from JS.</returns>
    protected async ValueTask<T> InvokeAsync<T>(string identifier, params object[]? args)
    {
        IJSObjectReference jsModule = await _jsModuleProvider.Value.ConfigureAwait(false);
        return await jsModule
            .InvokeAsync<T>(identifier, _cancellationTokenSource.Token, args)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        if (_jsModuleProvider.IsValueCreated)
        {
            IJSObjectReference module = await _jsModuleProvider.Value.ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
