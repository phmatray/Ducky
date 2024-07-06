namespace R3dux.Tests;


public class RootStateSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_Should_Work_Correctly()
    {
        // Arrange
        var rootState = new RootState();
        const string key = "test-key";
        var initialState = new TestState { Value = 42 };
        rootState.AddOrUpdateSliceState(key, initialState);
        var rootStateSerializer = new RootStateSerializer();

        // Act
        var json = rootStateSerializer.Serialize(rootState);
        var deserializedState = rootStateSerializer.Deserialize(json);
        
        // Assert
        deserializedState.ContainsKey(key).Should().BeTrue();
        deserializedState.Select<TestState>(key).Should().BeEquivalentTo(initialState);
    }

    [Fact]
    public void SaveAndLoadState_Should_Persist_State_Correctly()
    {
        // Arrange
        var rootState = new RootState();
        const string key = "test-key";
        var initialState = new TestState { Value = 42 };
        rootState.AddOrUpdateSliceState(key, initialState);
        var rootStateSerializer = new RootStateSerializer();

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            rootStateSerializer.SaveToFile(rootState, filePath);
            var loadedState = rootStateSerializer.LoadFromFile(filePath);

            // Assert
            loadedState.ContainsKey(key).Should().BeTrue();
            loadedState.Select<TestState>(key).Should().BeEquivalentTo(initialState);
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

    private class TestState
    {
        public int Value { get; set; }
    }
}
