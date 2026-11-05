using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Fakes;

/// <summary>
/// A JS→.NET stream (SPEC §11.5, §17.2): its length is known before it is opened, opening it above
/// <c>maxAllowedSize</c> faults as the real reference does, and it records whether it was opened and disposed.
/// </summary>
internal sealed class FakeJsStreamReference(byte[] content) : IJSStreamReference
{
    public long Length => content.Length;

    public bool Disposed { get; private set; }

    public bool Opened { get; private set; }

    /// <summary>Thrown by <see cref="DisposeAsync"/>, if set, once disposal is recorded (a circuit dropped meanwhile).</summary>
    public Exception? OnDispose { get; init; }

    /// <summary>
    /// If set, opening waits for it and the stream then ignores the caller's token: a read already under way when its
    /// token is cancelled still completes.
    /// </summary>
    public Task? Opening { get; init; }

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        Opened = true;
        return content.Length > maxAllowedSize
            ? ValueTask.FromException<Stream>(new ArgumentOutOfRangeException(nameof(maxAllowedSize)))
            : Opening is null ? new(new MemoryStream(content, writable: false)) : new(OpenLateAsync(Opening));
    }

    private async Task<Stream> OpenLateAsync(Task opening)
    {
        await opening;
        return new UncancellableStream(content);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return OnDispose is null ? default : ValueTask.FromException(OnDispose);
    }
}

internal sealed class UncancellableStream(byte[] content) : MemoryStream(content, writable: false)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer, CancellationToken.None);
}
