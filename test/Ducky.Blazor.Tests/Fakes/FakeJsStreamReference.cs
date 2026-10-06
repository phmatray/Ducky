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

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        Opened = true;
        return content.Length > maxAllowedSize
            ? ValueTask.FromException<Stream>(new ArgumentOutOfRangeException(nameof(maxAllowedSize)))
            : new(new MemoryStream(content, writable: false));
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return OnDispose is null ? default : ValueTask.FromException(OnDispose);
    }
}
