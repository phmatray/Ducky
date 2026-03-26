// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Shouldly;

namespace Ducky.Tests.Core;

public class MemoizedSelectorTests
{
    [Fact]
    public void Create_SameStateReference_ShouldNotCallDependencyFunctions()
    {
        // Arrange
        int dependencyCallCount = 0;
        Func<TestState, int> selector = MemoizedSelector.Create<TestState, int>(
            state => state.Value * 2,
            state =>
            {
                dependencyCallCount++;
                return state.Value;
            });

        TestState state = new(5);

        // Act — first call populates cache
        int result1 = selector(state);
        int callsAfterFirst = dependencyCallCount;

        // Second call with same reference should short-circuit
        int result2 = selector(state);

        // Assert
        result1.ShouldBe(10);
        result2.ShouldBe(10);
        dependencyCallCount.ShouldBe(callsAfterFirst); // No additional calls
    }

    private record TestState(int Value);
}
