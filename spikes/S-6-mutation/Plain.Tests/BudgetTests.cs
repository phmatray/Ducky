using Plain;

namespace Plain.Tests;

public sealed class BudgetTests
{
    [Fact]
    public void TryTake_WithinCapacity_Takes()
    {
        var budget = new Budget(3);

        budget.TryTake(2).ShouldBeTrue();
        budget.TryTake(1).ShouldBeTrue();
        budget.Remaining.ShouldBe(0);
    }

    [Fact]
    public void TryTake_OverCapacity_Refuses()
    {
        var budget = new Budget(3);
        budget.TryTake(2).ShouldBeTrue();

        budget.TryTake(2).ShouldBeFalse();
        budget.Remaining.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryTake_NotPositive_Throws(int amount) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Budget(3).TryTake(amount));

    [Fact]
    public async Task AsyncShapes_CompletedAmounts_Take()
    {
        var budget = new Budget(8);

        (await budget.TryTakeWithOptionsAsync(() => Task.FromResult(3))).ShouldBeTrue();
        (await budget.TryTakeValueAsync(() => ValueTask.FromResult(4))).ShouldBeTrue();
        budget.Remaining.ShouldBe(1);

        var lease = new Lease();
        (await budget.ReleaseAsync(lease)).ShouldBe(7);
        lease.Disposed.ShouldBeTrue();
        budget.Remaining.ShouldBe(8);
    }

    [Fact]
    public async Task AsyncShapes_NullArguments_Throw()
    {
        var budget = new Budget(1);

        await Should.ThrowAsync<ArgumentNullException>(() => budget.TryTakeWithOptionsAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(budget.TryTakeValueAsync(null!).AsTask());
        await Should.ThrowAsync<ArgumentNullException>(() => budget.ReleaseAsync(null!));
    }

    private sealed class Lease : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
