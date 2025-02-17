// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Ducky.Tests.Extensions.Operators;

[SuppressMessage("Roslynator", "RCS1046:Asynchronous method name should end with \'Async\'")]
public class ReactiveSelectorExtensionsTests
{
    private static readonly int[] SourceArray = [1, 2, 3];

    [Fact]
    public async Task SwitchSelect_ShouldSwitchToLatestObservable()
    {
        // Arrange
        Observable<int> input = SourceArray.ToObservable();

        // Act
        string[] result = await input
            .SwitchSelect(Selector)
            .ToArrayAsync()
            .ConfigureAwait(true);

        // Assert
        string[] expected = ["Selected: 3"];
        result.ShouldBeEquivalentTo(expected);
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
        Observable<int> input = SourceArray.ToObservable();

        // Act
        string[] result = await input
            .ConcatSelect(Selector)
            .ToArrayAsync()
            .ConfigureAwait(true);

        // Assert
        string[] expected =
        [
            "Selected: 1",
            "Selected: 2",
            "Selected: 3"
        ];

        result.ShouldBeEquivalentTo(expected);
        return;

        static Observable<string> Selector(int x)
            => Observable.Return($"Selected: {x}");
    }

    [Fact]
    public async Task MergeSelect_ShouldMergeAllObservableSequences()
    {
        // Arrange
        Observable<int> input = SourceArray.ToObservable();

        // Act
        string[] result = await input
            .MergeSelect(Selector)
            .ToArrayAsync()
            .ConfigureAwait(true);

        // Assert
        string[] expected =
        [
            "Selected: 1",
            "Selected: 2",
            "Selected: 3"
        ];
        
        result.ShouldBeEquivalentTo(expected);
        return;

        static Observable<string> Selector(int x)
            => Observable.Return($"Selected: {x}");
    }
}
