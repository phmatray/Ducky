using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Web;

namespace Web.Tests;

public sealed class SnapshotStoreTests
{
    private readonly FakeLogger<SnapshotStore> _logger = new();

    [Fact]
    public void Accept_NewerVersion_Replaces()
    {
        var store = new SnapshotStore(_logger);

        store.Accept("""{"Key":"cart","Version":1}""").ShouldBeTrue();
        store.Accept("""{"Key":"cart","Version":2}""").ShouldBeTrue();
        store.Accept("""{"Key":"user","Version":1}""").ShouldBeTrue();

        store.Count.ShouldBe(2);
        store.Export("cart").ShouldBe("""{"Key":"cart","Version":2}""");
        _logger.Collector.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Accept_SameOrOlderVersion_RejectsAndLogs(int version)
    {
        var store = new SnapshotStore(_logger);
        store.Accept("""{"Key":"cart","Version":2}""");

        store.Accept($$"""{"Key":"cart","Version":{{version}}}""").ShouldBeFalse();

        store.Export("cart").ShouldBe("""{"Key":"cart","Version":2}""");
        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe($"Snapshot cart v{version} rejected: v2 is stored");
    }

    [Fact]
    public void Accept_NullJson_Throws() =>
        Should.Throw<ArgumentException>(() => new SnapshotStore(_logger).Accept("null")).Message.ShouldBe("null snapshot (Parameter 'json')");

    [Fact]
    public async Task AcceptAsync_Loader_Accepts()
    {
        var store = new SnapshotStore(_logger);

        (await store.AcceptAsync(() => Task.FromResult("""{"Key":"cart","Version":1}"""))).ShouldBeTrue();
        await Should.ThrowAsync<ArgumentNullException>(() => store.AcceptAsync(null!));
        store.Count.ShouldBe(1);
    }
}
