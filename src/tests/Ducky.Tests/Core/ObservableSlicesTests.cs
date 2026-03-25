// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable SA1402, SA1649

namespace Ducky.Tests.Core;

public sealed class ObservableSlicesTests : IDisposable
{
    private readonly ObservableSlices _sut = new();
    private bool _disposed;

    [Fact]
    public void Dispose_Should_Unsubscribe_All_Handlers_With_Multiple_Slices()
    {
        // Arrange
        SliceA sliceA = new();
        SliceB sliceB = new();
        SliceC sliceC = new();

        _sut.AddSlice(sliceA);
        _sut.AddSlice(sliceB);
        _sut.AddSlice(sliceC);

        int eventCount = 0;
        _sut.SliceStateChanged += (_, _) => eventCount++;

        // Verify subscriptions are working before dispose
        sliceA.OnDispatch(new TestIncrementAction());
        sliceB.OnDispatch(new TestIncrementAction());
        sliceC.OnDispatch(new TestIncrementAction());
        eventCount.ShouldBe(3);

        // Act
        _sut.Dispose();

        // Reset counter and dispatch again
        eventCount = 0;
        sliceA.OnDispatch(new TestIncrementAction());
        sliceB.OnDispatch(new TestIncrementAction());
        sliceC.OnDispatch(new TestIncrementAction());

        // Assert - no events should fire after dispose
        eventCount.ShouldBe(0);
    }

    [Fact]
    public void Dispose_Should_Handle_Empty_Slices()
    {
        // Act & Assert - should not throw
        _sut.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}

// Distinct slice types to ensure different dictionary keys
internal sealed record SliceA : SliceReducers<int>
{
    public SliceA()
    {
        On<TestIncrementAction>((state, _) => state + 1);
    }

    public override int GetInitialState() => 0;
}

internal sealed record SliceB : SliceReducers<long>
{
    public SliceB()
    {
        On<TestIncrementAction>((state, _) => state + 1);
    }

    public override long GetInitialState() => 0;
}

internal sealed record SliceC : SliceReducers<double>
{
    public SliceC()
    {
        On<TestIncrementAction>((state, _) => state + 1);
    }

    public override double GetInitialState() => 0;
}

#pragma warning restore SA1402, SA1649
