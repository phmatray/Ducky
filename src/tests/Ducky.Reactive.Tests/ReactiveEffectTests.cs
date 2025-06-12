// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Reactive.Middlewares.ReactiveEffects;

namespace Ducky.Reactive.Tests;

public class ReactiveEffectTests
{
    [Fact]
    public void GetKey_ShouldReturnTypeName()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        string key = effect.GetKey();

        // Assert
        key.ShouldBe("TestEffect");
    }

    [Fact]
    public void GetAssemblyName_ShouldReturnAssemblyName()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        string assemblyName = effect.GetAssemblyName();

        // Assert
        assemblyName.ShouldBe("Ducky.Reactive.Tests");
    }

    [Fact]
    public void Handle_DefaultImplementation_ShouldReturnEmptyObservable()
    {
        // Arrange
        BaseTestEffect effect = new();
        IObservable<object> actions = Observable.Never<object>();
        IObservable<IRootState> rootState = Observable.Never<IRootState>();
        List<object> results = [];

        // Act
        IObservable<object> result = effect.Handle(actions, rootState);
        result.Subscribe(results.Add);

        // Wait a bit to ensure nothing is emitted
        Thread.Sleep(50);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void TimeProvider_ShouldUseSystemDefault()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        TimeProvider timeProvider = effect.TimeProvider;

        // Assert
        timeProvider.ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void ReactiveEffect_ShouldHaveTimeProvider()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        TimeProvider timeProvider = effect.TimeProvider;

        // Assert
        timeProvider.ShouldNotBeNull();
        timeProvider.GetType().BaseType.ShouldBe(typeof(TimeProvider));
    }

    [Fact]
    public void ReactiveEffect_Inheritance_ShouldWorkCorrectly()
    {
        // Arrange
        ComplexTestEffect effect = new();

        // Act
        string key = effect.GetKey();
        string assemblyName = effect.GetAssemblyName();

        // Assert
        key.ShouldBe("ComplexTestEffect");
        assemblyName.ShouldBe("Ducky.Reactive.Tests");
        effect.WasInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Multiple_ReactiveEffects_ShouldHaveUniqueKeys()
    {
        // Arrange
        ReactiveEffect[] effects = 
        [
            new TestEffect(),
            new BaseTestEffect(),
            new ComplexTestEffect()
        ];

        // Act
        string[] keys = effects.Select(e => e.GetKey()).ToArray();

        // Assert
        keys.ShouldBeUnique();
        keys.ShouldBe(["TestEffect", "BaseTestEffect", "ComplexTestEffect"]);
    }

    // Test helper classes
    private class TestEffect : ReactiveEffect
    {
    }

    private class BaseTestEffect : ReactiveEffect
    {
        // Uses default Handle implementation
    }

    private class ComplexTestEffect : ReactiveEffect
    {
        public bool WasInitialized { get; private set; }

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            WasInitialized = true;
            return Observable.Empty<object>();
        }
    }
}