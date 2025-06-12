// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class ReactiveSelectorExtensionsTests
{
    [Fact]
    public void SwitchSelect_ShouldSwitchToLatestObservableSequence()
    {
        // Arrange
        Subject<int> source = new();
        Subject<string>[] innerSubjects = [new(), new(), new()];
        List<string> results = [];

        // Act
        source
            .SwitchSelect(i => innerSubjects[i])
            .Subscribe(results.Add);

        // First inner observable
        source.OnNext(0);
        innerSubjects[0].OnNext("A0");
        innerSubjects[0].OnNext("B0");

        // Switch to second inner observable
        source.OnNext(1);
        innerSubjects[0].OnNext("C0"); // This should be ignored
        innerSubjects[1].OnNext("A1");

        // Switch to third inner observable
        source.OnNext(2);
        innerSubjects[1].OnNext("B1"); // This should be ignored
        innerSubjects[2].OnNext("A2");
        innerSubjects[2].OnNext("B2");

        // Assert
        results.ShouldBe(["A0", "B0", "A1", "A2", "B2"]);
    }

    [Fact]
    public void ConcatSelect_ShouldConcatenateAllObservableSequences()
    {
        // Arrange
        Subject<int> source = new();
        List<string> results = [];

        // Act
        source
            .ConcatSelect(i => Observable.Range(1, 3).Select(j => $"{i}-{j}"))
            .Subscribe(results.Add);

        source.OnNext(10);
        source.OnNext(20);
        source.OnNext(30);
        source.OnCompleted();

        // Allow observables to complete
        Thread.Sleep(50);

        // Assert
        results.ShouldBe([
            "10-1", "10-2", "10-3",
            "20-1", "20-2", "20-3",
            "30-1", "30-2", "30-3"
        ]);
    }

    [Fact]
    public void MergeSelect_ShouldMergeAllObservableSequences()
    {
        // Arrange
        Subject<int> source = new();
        List<int> results = [];

        // Act
        source
            .MergeSelect(i => Observable.Return(i * 10).Delay(TimeSpan.FromMilliseconds(i)))
            .Subscribe(results.Add);

        source.OnNext(3);
        source.OnNext(1);
        source.OnNext(2);
        source.OnCompleted();

        // Wait for all to complete
        Thread.Sleep(100);

        // Assert - Results should contain all values (order may vary)
        results.Count.ShouldBe(3);
        results.ShouldContain(10);
        results.ShouldContain(20);
        results.ShouldContain(30);
    }

    [Fact]
    public void SwitchSelect_WithCompletingInnerSequences_ShouldHandleCorrectly()
    {
        // Arrange
        Subject<string> source = new();
        List<char> results = [];

        // Act
        source
            .SwitchSelect(s => s.ToObservable())
            .Subscribe(results.Add);

        source.OnNext("abc");
        Thread.Sleep(10); // Let it complete

        source.OnNext("xyz");
        Thread.Sleep(10); // Let it complete

        // Assert
        results.ShouldBe(['a', 'b', 'c', 'x', 'y', 'z']);
    }

    [Fact]
    public void ConcatSelect_WithEmptySource_ShouldProduceEmptyResult()
    {
        // Arrange
        IObservable<int> source = Observable.Empty<int>();
        List<string> results = [];

        // Act
        source
            .ConcatSelect(i => Observable.Return(i.ToString()))
            .Subscribe(results.Add);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void MergeSelect_WithError_ShouldPropagateError()
    {
        // Arrange
        Subject<int> source = new();
        List<string> results = [];
        Exception? capturedError = null;

        // Act
        source
            .MergeSelect(i => i == 2
                ? Observable.Throw<string>(new InvalidOperationException("Test error"))
                : Observable.Return($"Value: {i}"))
            .Subscribe(
                onNext: results.Add,
                onError: err => capturedError = err);

        source.OnNext(1);
        source.OnNext(2); // This will throw
        source.OnNext(3); // This won't be processed

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBe("Value: 1");
        capturedError.ShouldNotBeNull();
        capturedError.ShouldBeOfType<InvalidOperationException>();
        capturedError.Message.ShouldBe("Test error");
    }

    [Fact]
    public void AllOperators_WithComplex_Scenario()
    {
        // Arrange
        Subject<string> commands = new();
        List<string> results = [];

        // Simulate different command processing strategies
        var switchResults = new List<string>();
        var concatResults = new List<string>();
        var mergeResults = new List<string>();

        // Act - Setup different processing pipelines
        commands
            .SwitchSelect(cmd => ProcessCommandAsync(cmd, "switch"))
            .Subscribe(switchResults.Add);

        commands
            .ConcatSelect(cmd => ProcessCommandAsync(cmd, "concat"))
            .Subscribe(concatResults.Add);

        commands
            .MergeSelect(cmd => ProcessCommandAsync(cmd, "merge"))
            .Subscribe(mergeResults.Add);

        // Send commands
        commands.OnNext("fast");
        commands.OnNext("slow");
        commands.OnCompleted();

        // Wait for processing
        Thread.Sleep(200);

        // Assert
        // Switch: only processes the last command fully
        switchResults.Count.ShouldBeLessThanOrEqualTo(2);

        // Concat: processes all commands in order
        concatResults.Count.ShouldBe(4); // 2 commands × 2 results each

        // Merge: processes all commands concurrently
        mergeResults.Count.ShouldBe(4);
    }

    private static IObservable<string> ProcessCommandAsync(string command, string mode)
    {
        int delay = command == "slow" ? 50 : 10;
        return Observable.Range(1, 2)
            .Select(i => $"{mode}-{command}-{i}")
            .SelectMany(x => Observable.Return(x).Delay(TimeSpan.FromMilliseconds(delay)));
    }
}