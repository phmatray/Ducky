using System.ComponentModel;

namespace Probe;

/// <summary>Async state machine: both <c>IsCompleted</c> branches of each await.</summary>
public static class AsyncShapes
{
    /// <summary>Awaits <paramref name="value"/> and adds <paramref name="add"/>.</summary>
    public static async Task<int> AddAsync(Task<int> value, int add) => await value.ConfigureAwait(false) + add;

    /// <summary><c>await using</c>: lowered try/finally with a null check and a DisposeAsync await.</summary>
    public static async Task<int> UseAsync(Resource resource)
    {
        await using (resource.ConfigureAwait(false))
        {
            await resource.Ready.ConfigureAwait(false);
            return resource.Id;
        }
    }
}

/// <summary>The same shapes as <see cref="AsyncShapes"/>, called by the tests with completed tasks only.</summary>
public static class SyncOnlyAsyncShapes
{
    /// <summary>Awaits <paramref name="value"/> and adds <paramref name="add"/>; never suspends in the tests.</summary>
    public static async Task<int> AddAsync(Task<int> value, int add) => await value.ConfigureAwait(false) + add;

    /// <summary><c>await using</c> over a resource whose tasks are already complete in the tests.</summary>
    public static async Task<int> UseAsync(Resource resource)
    {
        await using (resource.ConfigureAwait(false))
        {
            await resource.Ready.ConfigureAwait(false);
            return resource.Id;
        }
    }
}

/// <summary>An async disposable whose readiness and disposal the test gates.</summary>
public sealed class Resource(int id, Task ready, Task disposal) : IAsyncDisposable
{
    /// <summary>The id returned by <see cref="AsyncShapes.UseAsync"/>.</summary>
    public int Id { get; } = id;

    /// <summary>Awaited inside the <c>await using</c> block.</summary>
    public Task Ready { get; } = ready;

    /// <summary>Whether <see cref="DisposeAsync"/> ran.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await disposal.ConfigureAwait(false);
        Disposed = true;
    }
}

/// <summary>Records: synthesized Equals, GetHashCode, PrintMembers, ToString, Deconstruct and clone.</summary>
public sealed record Point(int X, int Y)
{
    /// <summary>A non-positional member with a body.</summary>
    public int Sum => X + Y;
}

/// <summary>A non-sealed record: virtual EqualityContract and a protected copy constructor.</summary>
public record Shape(string Name);

/// <summary>A derived record: base calls in Equals and PrintMembers.</summary>
public sealed record Circle(string Name, double Radius) : Shape(Name);

/// <summary>Lowered <c>lock</c> statements, on a <see cref="Lock"/> and on an object (Monitor with lockTaken).</summary>
public sealed class Counter
{
    private readonly Lock _gate = new();
#pragma warning disable CA2002 // justification: spike probe of the Monitor lowering (lockTaken branch in finally)
    private readonly object _monitor = new();
#pragma warning restore CA2002
    private int _count;

    /// <summary>Increments under a <see cref="Lock"/>.</summary>
    public int Increment()
    {
        lock (_gate)
        {
            return ++_count;
        }
    }

    /// <summary>Decrements under a Monitor lock.</summary>
    public int Decrement()
    {
        lock (_monitor)
        {
            return --_count;
        }
    }
}

/// <summary>Pattern-matching switch expressions with a default arm.</summary>
public static class Switches
{
    /// <summary>Type, guard and discard arms.</summary>
    public static int Classify(object? value) => value switch
    {
        int i when i > 0 => 1,
        int => 0,
        string s => s.Length,
        _ => -1,
    };

    /// <summary>An enum switch; CS8524 (unnamed values) requires the throwing default arm.</summary>
    public static string Name(DayKind kind) => kind switch
    {
        DayKind.Weekday => "weekday",
        DayKind.Weekend => "weekend",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>Two cases, switched exhaustively above.</summary>
public enum DayKind
{
    /// <summary>Monday to Friday.</summary>
    Weekday,

    /// <summary>Saturday and Sunday.</summary>
    Weekend,
}

/// <summary>The tombstone shape (SPEC §22.1): member-less, error-level obsolete, hidden.</summary>
[Obsolete("Removed in 2.0.", error: true, DiagnosticId = "S2M001", UrlFormat = "https://example.invalid/{0}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IOldThing;
