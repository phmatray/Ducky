namespace R3dux.Tests;


public class RootStateSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_Should_Work_Correctly()
    {
        // Arrange
        var rootState = new RootState();
        const string key = "testKey";
        var initialState = new TestState { Value = 42 };
        rootState.AddOrUpdateSliceState(key, initialState);

        // Act
        var json = RootStateSerializer.Serialize(rootState);
        var deserializedState = RootStateSerializer.Deserialize(json);
        
        // Assert
        deserializedState.ContainsKey(key).Should().BeTrue();
        deserializedState.Select<TestState>(key).Should().BeEquivalentTo(initialState);
    }

    [Fact]
    public void SaveAndLoadState_Should_Persist_State_Correctly()
    {
        // Arrange
        var rootState = new RootState();
        const string key = "testKey";
        var initialState = new TestState { Value = 42 };
        rootState.AddOrUpdateSliceState(key, initialState);

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            RootStateSerializer.SaveToFile(rootState, filePath);
            var loadedState = RootStateSerializer.LoadFromFile(filePath);

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
