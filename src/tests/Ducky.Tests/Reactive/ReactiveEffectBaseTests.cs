// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class ReactiveEffectBaseTests
{
    [Fact]
    public async Task DisposeAsync_ShouldCallOnDisposeAsync_WithoutBlocking()
    {
        // Arrange
        var effect = new TestAsyncDisposableEffect();

        // Act
        await ((IAsyncDisposable)effect).DisposeAsync();

        // Assert
        effect.DisposeAsyncCalled.ShouldBeTrue();
        effect.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var effect = new TestAsyncDisposableEffect();

        // Act
        await ((IAsyncDisposable)effect).DisposeAsync();
        await ((IAsyncDisposable)effect).DisposeAsync();

        // Assert
        effect.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotCallOnDisposeAsync()
    {
        // Arrange
        var effect = new TestAsyncDisposableEffect();

        // Act
        ((IDisposable)effect).Dispose();

        // Assert
        effect.DisposeAsyncCalled.ShouldBeFalse();
        effect.IsDisposed.ShouldBeTrue();
    }

    public class TestAsyncDisposableEffect : ReactiveEffectBase
    {
        public bool DisposeAsyncCalled { get; private set; }

        protected override async Task OnDisposeAsync()
        {
            DisposeAsyncCalled = true;
            await Task.CompletedTask;
        }

        protected override IObservable<object> HandleCore(
            IObservable<object> actions, IObservable<IStateProvider> stateProvider)
            => Observable.Empty<object>();
    }
}
