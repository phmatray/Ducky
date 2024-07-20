using R3dux.Tests.TestModels;

namespace R3dux.Tests.Services;

public class RootStateSerializerTests
{
    private const string Key = "test-key";
    
    private const string JsonString =
        """
        {
          "test-key": {
            "type": "R3dux.Tests.TestModels.TestState, R3dux.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
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
        var json = _sut.Serialize(_rootState);
        
        // Assert
        json.Should().BeEquivalentTo(JsonString);
    }
    
    [Fact]
    public void Deserialize_Should_Work_Correctly()
    {
        // Act
        var deserializedState = _sut.Deserialize(JsonString);
        
        // Assert
        deserializedState.ContainsKey(Key).Should().BeTrue();
        deserializedState.GetSliceState<TestState>(Key).Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void SerializeAndDeserialize_Should_Work_Correctly()
    {
        // Act
        var json = _sut.Serialize(_rootState);
        var deserializedState = _sut.Deserialize(json);
        
        // Assert
        deserializedState.ContainsKey(Key).Should().BeTrue();
        deserializedState.GetSliceState<TestState>(Key).Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void SaveAndLoadState_Should_Persist_State_Correctly()
    {
        // Arrange
        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            _sut.SaveToFile(_rootState, filePath);
            var loadedState = _sut.LoadFromFile(filePath);

            // Assert
            loadedState.ContainsKey(Key).Should().BeTrue();
            loadedState.GetSliceState<TestState>(Key).Should().BeEquivalentTo(_initialState);
        }
        finally
        {
            // Clean up
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
