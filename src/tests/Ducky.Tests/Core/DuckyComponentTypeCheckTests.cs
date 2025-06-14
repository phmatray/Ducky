// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

public class DuckyComponentTypeCheckTests
{
    [Fact]
    public void RootState_Type_Check_Should_Work()
    {
        // This test verifies the fix for DuckyComponent<RootState>

        // Test 1: Verify RootState implements IStateProvider
        typeof(RootState).GetInterfaces().ShouldContain(typeof(IStateProvider));

        // Test 2: Verify typeof(RootState) != typeof(IStateProvider)
        (typeof(RootState) == typeof(IStateProvider)).ShouldBeFalse();

        // Test 3: Verify RootState can be used as IStateProvider
        IStateProvider stateProvider = new RootState(ImmutableSortedDictionary<string, object>.Empty);
        stateProvider.ShouldNotBeNull();
    }

    [Fact]
    public void GetSliceState_WithRootState_Should_Throw()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky();
        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();

        // Act & Assert - calling GetSlice<RootState>() should throw
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => store.GetSlice<RootState>());
        exception.Message.ShouldContain("Slice of type RootState not found", Case.Insensitive);
    }

    [Fact]
    public void Store_Should_Implement_IStateProvider()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky();
        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();

        // Act & Assert
        store.ShouldBeAssignableTo<IStateProvider>();
    }
}
