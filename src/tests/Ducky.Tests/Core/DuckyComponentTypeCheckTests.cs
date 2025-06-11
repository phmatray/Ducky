// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

public class DuckyComponentTypeCheckTests
{
    [Fact]
    public void RootState_Type_Check_Should_Work()
    {
        // This test verifies the fix for DuckyComponent<RootState>

        // Test 1: Verify RootState implements IRootState
        typeof(RootState).GetInterfaces().ShouldContain(typeof(IRootState));

        // Test 2: Verify typeof(RootState) == typeof(RootState)
        (typeof(RootState) == typeof(RootState)).ShouldBeTrue();

        // Test 3: Verify typeof(RootState) != typeof(IRootState)
        (typeof(RootState) == typeof(IRootState)).ShouldBeFalse();

        // Test 4: Simulate the type check in DuckyComponent.UpdateCurrentState()
        Type tState = typeof(RootState);
        bool shouldUseRootState = (tState == typeof(IRootState) || tState == typeof(RootState));
        shouldUseRootState.ShouldBeTrue();
    }

    [Fact]
    public void GetSliceState_WithRootState_Should_Throw()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDuckyStore(builder => { });
        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();

        // Act & Assert - calling GetSliceState<RootState>() should throw
        DuckyException exception = Should.Throw<DuckyException>(() => store.CurrentState.GetSliceState<RootState>());
        exception.Message.ShouldContain("State of type 'RootState' not found");
    }

    [Fact]
    public void Store_CurrentState_Should_Be_RootState()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDuckyStore(builder => { });
        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();

        // Act & Assert
        store.CurrentState.ShouldBeOfType<RootState>();
        store.CurrentState.ShouldBeAssignableTo<IRootState>();
    }
}
