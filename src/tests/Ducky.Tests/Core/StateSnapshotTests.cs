// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable SA1402, SA1649

namespace Ducky.Tests.Core;

/// <summary>
/// Tests for O(1) StateSnapshot lookups via type-indexed dictionary.
/// StateSnapshot is internal; behavior is verified through ObservableSlices.GetSnapshotData()
/// and through IStateProvider operations on the returned snapshot data.
/// </summary>
public sealed class StateSnapshotTests : IDisposable
{
    private readonly ObservableSlices _slices = new();

    public void Dispose()
    {
        _slices.Dispose();
    }

    [Fact]
    public void GetSnapshotData_WithNoSlices_ReturnsEmptyCollections()
    {
        // Act
        (ImmutableSortedDictionary<string, object> state, Dictionary<Type, string> typeIndex) = _slices.GetSnapshotData();

        // Assert
        state.ShouldBeEmpty();
        typeIndex.ShouldBeEmpty();
    }

    [Fact]
    public void GetSnapshotData_WithSlices_BuildsTypeIndex()
    {
        // Arrange
        _slices.AddSlice(new SnapshotSliceInt());
        _slices.AddSlice(new SnapshotSliceString());

        // Act
        (ImmutableSortedDictionary<string, object> state, Dictionary<Type, string> typeIndex) = _slices.GetSnapshotData();

        // Assert
        state.Count.ShouldBe(2);
        typeIndex.ShouldContainKey(typeof(SnapshotIntState));
        typeIndex.ShouldContainKey(typeof(SnapshotStringState));
    }

    [Fact]
    public void GetSnapshotData_IsCachedWhenNotDirty()
    {
        // Arrange
        _slices.AddSlice(new SnapshotSliceInt());

        // Act - call twice
        (ImmutableSortedDictionary<string, object> state1, Dictionary<Type, string> typeIndex1) = _slices.GetSnapshotData();
        (ImmutableSortedDictionary<string, object> state2, Dictionary<Type, string> typeIndex2) = _slices.GetSnapshotData();

        // Assert - same reference returned (cache hit)
        state1.ShouldBeSameAs(state2);
        typeIndex1.ShouldBeSameAs(typeIndex2);
    }

    [Fact]
    public void GetSnapshotData_IsInvalidatedWhenSliceStateChanges()
    {
        // Arrange
        SnapshotSliceInt slice = new();
        _slices.AddSlice(slice);
        ImmutableSortedDictionary<string, object> state1 = _slices.GetSnapshotData().State;

        // Act - trigger state change
        slice.OnDispatch(new SnapshotIncrementAction());
        ImmutableSortedDictionary<string, object> state2 = _slices.GetSnapshotData().State;

        // Assert - new dictionary built after state changed
        state1.ShouldNotBeSameAs(state2);
    }

    [Fact]
    public void GetStateDictionary_UsesCachedVersion()
    {
        // Arrange
        _slices.AddSlice(new SnapshotSliceInt());

        // Act
        ImmutableSortedDictionary<string, object> dict1 = _slices.GetStateDictionary();
        ImmutableSortedDictionary<string, object> dict2 = _slices.GetStateDictionary();

        // Assert - same reference (backed by same cache)
        dict1.ShouldBeSameAs(dict2);
    }

    [Fact]
    public void GetSnapshotData_TypeIndex_MapsToCorrectKey()
    {
        // Arrange
        SnapshotSliceInt slice = new();
        _slices.AddSlice(slice);

        // Act
        (ImmutableSortedDictionary<string, object> state, Dictionary<Type, string> typeIndex) = _slices.GetSnapshotData();

        // Assert
        typeIndex.TryGetValue(typeof(SnapshotIntState), out string? key).ShouldBeTrue();
        key.ShouldNotBeNullOrEmpty();
        state[key!].ShouldBeOfType<SnapshotIntState>();
    }

    [Fact]
    public void GetSnapshotData_IsInvalidatedWhenNewSliceAdded()
    {
        // Arrange
        _slices.AddSlice(new SnapshotSliceInt());
        Dictionary<Type, string> typeIndex1 = _slices.GetSnapshotData().TypeIndex;

        // Act - add another slice
        _slices.AddSlice(new SnapshotSliceString());
        Dictionary<Type, string> typeIndex2 = _slices.GetSnapshotData().TypeIndex;

        // Assert - cache was rebuilt
        typeIndex1.ShouldNotBeSameAs(typeIndex2);
        typeIndex2.Count.ShouldBe(2);
    }
}

internal record SnapshotIncrementAction;

internal record SnapshotIntState(int Count = 0);

internal sealed record SnapshotSliceInt : SliceReducers<SnapshotIntState>
{
    public SnapshotSliceInt()
    {
        On<SnapshotIncrementAction>((state, _) => state with { Count = state.Count + 1 });
    }

    public override SnapshotIntState GetInitialState() => new();
}

internal record SnapshotStringState(string Value = "initial");

internal sealed record SnapshotSliceString : SliceReducers<SnapshotStringState>
{
    public SnapshotSliceString()
    {
        On<SnapshotIncrementAction>((state, _) => state with { Value = "incremented" });
    }

    public override SnapshotStringState GetInitialState() => new();
}

#pragma warning restore SA1402, SA1649
