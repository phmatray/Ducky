// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;

namespace Ducky.Reactive.Tests;

public class ObservableExtensionsTests
{
    [Fact]
    public void FirstSync_WithElementInSequence_ShouldReturnFirstElement()
    {
        // Arrange
        IObservable<int> observable = Observable.Return(42);

        // Act
        int result = observable.FirstSync();

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void FirstSync_WithCompletedEmptySequence_ShouldThrow()
    {
        // Arrange
        IObservable<int> observable = Observable.Empty<int>();

        // Act & Assert
        Exception thrown = Should.Throw<AggregateException>(() => observable.FirstSync());
        thrown.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void FirstSync_WithImmediateValue_ShouldReturnValue()
    {
        // Arrange
        IObservable<string> observable = Observable.Return("test");

        // Act
        string result = observable.FirstSync();

        // Assert
        result.ShouldBe("test");
    }

    [Fact]
    public void FirstSync_WithMultipleValues_ShouldReturnFirstOnly()
    {
        // Arrange
        IObservable<int> observable = Observable.Range(1, 5);

        // Act
        int result = observable.FirstSync();

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void FirstSync_WithError_ShouldThrowException()
    {
        // Arrange
        Exception testException = new InvalidOperationException("Test error");
        IObservable<int> observable = Observable.Throw<int>(testException);

        // Act & Assert
        Exception thrown = Should.Throw<AggregateException>(() => observable.FirstSync());
        thrown.InnerException
            .ShouldBeOfType<InvalidOperationException>()
            .Message
            .ShouldBe("Test error");
    }

    [Fact]
    public void FirstSync_WithDelayedValue_ShouldWaitAndReturn()
    {
        // Arrange
        IObservable<string> observable = Observable
            .Return("delayed")
            .Delay(TimeSpan.FromMilliseconds(50));

        // Act
        DateTime start = DateTime.UtcNow;
        string result = observable.FirstSync();
        TimeSpan elapsed = DateTime.UtcNow - start;

        // Assert
        result.ShouldBe("delayed");
        elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(40));
    }
}