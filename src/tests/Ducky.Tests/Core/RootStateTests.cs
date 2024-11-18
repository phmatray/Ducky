// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Core;

public class RootStateTests
{
    private const string NonExistingKey = "nonExistingKey";

    private readonly RootState _sut = Factories.CreateTestRootState();

    [Fact]
    public void Select_Should_Throw_Exception_If_State_Not_Found()
    {
        // Act
        Action act = () => _sut.GetSliceState<TestState>(NonExistingKey);

        // Assert
        act.Should().Throw<DuckyException>()
            .WithMessage($"State with key '{NonExistingKey}' is not of type 'TestState'.");
    }

    [Fact]
    public void ContainsKey_Should_Return_False_If_Key_Does_Not_Exist()
    {
        // Act
        var result = _sut.ContainsKey(NonExistingKey);

        // Assert
        result.Should().BeFalse();
    }
}
