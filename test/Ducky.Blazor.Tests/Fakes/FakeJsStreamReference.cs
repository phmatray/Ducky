using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Fakes;

/// <summary>
/// A JS→.NET stream (SPEC §11.5, §17.2): its length is known before it is opened, opening it above
/// <c>maxAllowedSize</c> faults as the real reference does, and it records its disposal.
/// </summary>
internal sealed class FakeJsStreamReference(byte[] content) : IJSStreamReference
{
    public long Length => content.Length;

    public bool Disposed { get; private set; }

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
        content.Length > maxAllowedSize
            ? ValueTask.FromException<Stream>(new ArgumentOutOfRangeException(nameof(maxAllowedSize)))
            : new(new MemoryStream(content, writable: false));

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return default;
    }
}
