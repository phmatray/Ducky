// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Services;

public class RootStateSerializerTests
{
    private const string Key = "test-key";

    private const string JsonString =
        """
        {
          "test-key": {
            "type": "Ducky.Tests.TestModels.TestState, Ducky.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            "value": {
              "value": 42
            }
          }
        }
        """;

    private readonly RootStateSerializer _sut = new();
    private readonly RootState _rootState = Factories.CreateTestRootState();
    private readonly TestState _initialState = new() { Value = 42 };

    [Fact]
    public void Serialize_Should_Work_Correctly()
    {
        // Act
        string json = _sut.Serialize(_rootState);

        // Assert
        json.ShouldContain("test-key");
        json.ShouldContain("Ducky.Tests.TestModels.TestState");
        json.ShouldContain("42");
    }

    [Fact]
    public void Deserialize_Should_Work_Correctly()
    {
        // Act
        IRootState deserializedState = _sut.Deserialize(JsonString);

        // Assert
        deserializedState.ContainsKey(Key).ShouldBeTrue();
        deserializedState.GetSliceState<TestState>(Key).ShouldBeEquivalentTo(_initialState);
    }

    [Fact]
    public void SerializeAndDeserialize_Should_Work_Correctly()
    {
        // Act
        string json = _sut.Serialize(_rootState);
        IRootState deserializedState = _sut.Deserialize(json);

        // Assert
        deserializedState.ContainsKey(Key).ShouldBeTrue();
        deserializedState.GetSliceState<TestState>(Key).ShouldBeEquivalentTo(_initialState);
    }
}
