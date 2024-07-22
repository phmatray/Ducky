// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.Extensions.Operators;

public class ReactiveSelectorExtensionsTests
{
    private static readonly int[] _sourceArray = [1, 2, 3];

    [Fact]
    public async Task SwitchSelect_ShouldSwitchToLatestObservable()
    {
        // Arrange
        var input = _sourceArray.ToObservable();

        // Act
        var result = await input.SwitchSelect((Func<int, Observable<string>>)Selector).ToArrayAsync();

        // Assert
        result.Should().BeEquivalentTo(
            ["Selected: 3"], options => options.WithStrictOrdering());
        return;

        static Observable<string> Selector(int x)
            => Observable
                .Interval(TimeSpan.FromMilliseconds(100))
                .Take(1)
                .Select(_ => $"Selected: {x}");
    }

    [Fact]
    public async Task ConcatSelect_ShouldConcatAllObservableSequences()
    {
        // Arrange
        var input = _sourceArray.ToObservable();

        // Act
        var result = await input.ConcatSelect((Func<int, Observable<string>>)Selector).ToArrayAsync();

        // Assert
        result.Should().BeEquivalentTo(
            ["Selected: 1", "Selected: 2", "Selected: 3"], options => options.WithStrictOrdering());
        return;

        static Observable<string> Selector(int x)
            => Observable.Return($"Selected: {x}");
    }

    [Fact]
    public async Task MergeSelect_ShouldMergeAllObservableSequences()
    {
        // Arrange
        var input = _sourceArray.ToObservable();

        // Act
        var result = await input.MergeSelect((Func<int, Observable<string>>)Selector).ToArrayAsync();

        // Assert
        result.Should().BeEquivalentTo(
            ["Selected: 1", "Selected: 2", "Selected: 3"], options => options.WithStrictOrdering());
        return;

        static Observable<string> Selector(int x)
            => Observable.Return($"Selected: {x}");
    }
}
