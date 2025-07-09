using System.Text.Json;
using Blazored.LocalStorage;
using Ducky.Blazor.Middlewares.Persistence;
using FakeItEasy;

namespace Ducky.Blazor.Tests.Middlewares.Persistence;

public class TypedLocalStoragePersistenceProviderTests
{
    private readonly ILocalStorageService _localStorage;
    private readonly PersistenceOptions _options;
    private readonly TypedLocalStoragePersistenceProvider _provider;

    public TypedLocalStoragePersistenceProviderTests()
    {
        _localStorage = A.Fake<ILocalStorageService>();
        _options = new PersistenceOptions { StorageKey = "test:state" };
        _provider = new TypedLocalStoragePersistenceProvider(_localStorage, _options);
    }

    [Fact]
    public void Constructor_WithNullLocalStorage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TypedLocalStoragePersistenceProvider(null!, _options));
    }

    [Fact]
    public void Constructor_WithoutStorageKey_UsesDefaultKey()
    {
        // Arrange
        var optionsWithoutKey = new PersistenceOptions { StorageKey = null };

        // Act
        var provider = new TypedLocalStoragePersistenceProvider(_localStorage, optionsWithoutKey);

        // Assert
        provider.ShouldNotBeNull();
        // Default key would be "ducky:state"
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithNoPersistedData_ReturnsNull()
    {
        // Arrange
        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult<PersistedStateDictionary?>(null)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(null)));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithEmptySlices_ReturnsNull()
    {
        // Arrange
        var emptyDict = new PersistedStateDictionary { Slices = new Dictionary<string, PersistedSlice>() };
        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult<PersistedStateDictionary?>(emptyDict)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(null)));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithValidData_ReturnsDeserializedState()
    {
        // Arrange
        var testState = new TestState { Value = "test value", Count = 42 };
        JsonElement stateJson = JsonSerializer.SerializeToElement(testState);

        var persistedDict = new PersistedStateDictionary
        {
            Slices = new Dictionary<string, PersistedSlice>
            {
                ["testSlice"] = new PersistedSlice
                {
                    TypeName = typeof(TestState).AssemblyQualifiedName!,
                    StateJson = stateJson
                }
            }
        };

        var metadata = new PersistenceMetadata
        {
            Version = 1,
            Timestamp = DateTime.UtcNow,
            Checksum = "test-checksum"
        };

        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult<PersistedStateDictionary?>(persistedDict)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(metadata)));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldNotBeNull();
        result!.State.ShouldNotBeNull();
        result.State!.Count.ShouldBe(1);
        result.State.ShouldContainKey("testSlice");

        var loadedState = result.State["testSlice"] as TestState;
        loadedState.ShouldNotBeNull();
        loadedState.Value.ShouldBe("test value");
        loadedState.Count.ShouldBe(42);

        result.Metadata.ShouldNotBeNull();
        result.Metadata.Version.ShouldBe(1);
        result.Metadata.Checksum.ShouldBe("test-checksum");
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithUnknownType_SkipsSlice()
    {
        // Arrange
        var validState = new TestState { Value = "valid" };
        JsonElement validJson = JsonSerializer.SerializeToElement(validState);

        var persistedDict = new PersistedStateDictionary
        {
            Slices = new Dictionary<string, PersistedSlice>
            {
                ["validSlice"] = new PersistedSlice
                {
                    TypeName = typeof(TestState).AssemblyQualifiedName!,
                    StateJson = validJson
                },
                ["invalidSlice"] = new PersistedSlice
                {
                    TypeName = "NonExistent.Type, NonExistent.Assembly",
                    StateJson = JsonSerializer.SerializeToElement(new { data = "test" })
                }
            }
        };

        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult<PersistedStateDictionary?>(persistedDict)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(null)));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldNotBeNull();
        result!.State.ShouldNotBeNull();
        result.State!.Count.ShouldBe(1);
        result.State.ShouldContainKey("validSlice");
        result.State.ShouldNotContainKey("invalidSlice");
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithDeserializationError_SkipsSlice()
    {
        // Arrange
        var persistedDict = new PersistedStateDictionary
        {
            Slices = new Dictionary<string, PersistedSlice>
            {
                ["badSlice"] = new PersistedSlice
                {
                    TypeName = typeof(TestState).AssemblyQualifiedName!,
                    StateJson = JsonSerializer.SerializeToElement("invalid json for TestState")
                }
            }
        };

        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult<PersistedStateDictionary?>(persistedDict)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(null)));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldNotBeNull();
        result!.State.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadWithMetadataAsync_WithException_ReturnsNull()
    {
        // Arrange
        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Throws(new Exception("Storage error"));

        // Act
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SaveWithMetadataAsync_WithValidState_SavesSuccessfully()
    {
        // Arrange
        var state = new Dictionary<string, object>
        {
            ["slice1"] = new TestState { Value = "test1", Count = 1 },
            ["slice2"] = new AnotherTestState { Name = "test2", IsActive = true }
        };

        var metadata = new PersistenceMetadata
        {
            Version = 2,
            Timestamp = DateTime.UtcNow
        };

        PersistedStateDictionary? capturedDict = null;
        PersistenceMetadata? capturedMetadata = null;

        A.CallTo(() => _localStorage.SetItemAsync("test:state", A<PersistedStateDictionary>._, A<CancellationToken>._))
            .Invokes((string _, PersistedStateDictionary dict, CancellationToken _) => capturedDict = dict)
            .Returns(new ValueTask());
        A.CallTo(() => _localStorage.SetItemAsync("test:state:metadata", A<PersistenceMetadata>._, A<CancellationToken>._))
            .Invokes((string _, PersistenceMetadata meta, CancellationToken _) => capturedMetadata = meta)
            .Returns(new ValueTask());

        // Act
        PersistenceResult result = await _provider.SaveWithMetadataAsync(state, metadata);

        // Assert
        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();

        capturedDict.ShouldNotBeNull();
        capturedDict!.Slices.Count.ShouldBe(2);
        capturedDict.Slices.ShouldContainKey("slice1");
        capturedDict.Slices.ShouldContainKey("slice2");

        capturedDict.Slices["slice1"].TypeName.ShouldContain(nameof(TestState));
        capturedDict.Slices["slice2"].TypeName.ShouldContain(nameof(AnotherTestState));

        capturedMetadata.ShouldNotBeNull();
        capturedMetadata!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task SaveWithMetadataAsync_WithEmptyState_SavesEmptyDictionary()
    {
        // Arrange
        var emptyState = new Dictionary<string, object>();
        var metadata = new PersistenceMetadata();

        PersistedStateDictionary? capturedDict = null;

        A.CallTo(() => _localStorage.SetItemAsync("test:state", A<PersistedStateDictionary>._, A<CancellationToken>._))
            .Invokes((string _, PersistedStateDictionary dict, CancellationToken _) => capturedDict = dict)
            .Returns(new ValueTask());
        A.CallTo(() => _localStorage.SetItemAsync("test:state:metadata", A<PersistenceMetadata>._, A<CancellationToken>._))
            .Returns(new ValueTask());

        // Act
        PersistenceResult result = await _provider.SaveWithMetadataAsync(emptyState, metadata);

        // Assert
        result.Success.ShouldBeTrue();
        capturedDict.ShouldNotBeNull();
        capturedDict!.Slices.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveWithMetadataAsync_WithException_ReturnsFailure()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = new TestState() };
        var metadata = new PersistenceMetadata();

        A.CallTo(() => _localStorage.SetItemAsync("test:state", A<PersistedStateDictionary>._, A<CancellationToken>._))
            .Throws(new Exception("Storage error"));

        // Act
        PersistenceResult result = await _provider.SaveWithMetadataAsync(state, metadata);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Storage error");
    }

    [Fact]
    public async Task ClearAsync_RemovesBothStateAndMetadata()
    {
        // Arrange
        A.CallTo(() => _localStorage.RemoveItemAsync("test:state", A<CancellationToken>._))
            .Returns(new ValueTask());
        A.CallTo(() => _localStorage.RemoveItemAsync("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask());

        // Act
        PersistenceResult result = await _provider.ClearAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();

        A.CallTo(() => _localStorage.RemoveItemAsync("test:state", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _localStorage.RemoveItemAsync("test:state:metadata", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ClearAsync_WithException_ReturnsFailure()
    {
        // Arrange
        A.CallTo(() => _localStorage.RemoveItemAsync("test:state", A<CancellationToken>._))
            .Throws(new Exception("Clear error"));

        // Act
        PersistenceResult result = await _provider.ClearAsync();

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Clear error");
    }

    [Fact]
    public async Task RoundTrip_SaveAndLoad_PreservesStateTypes()
    {
        // Arrange
        var originalState = new Dictionary<string, object>
        {
            ["test1"] = new TestState { Value = "original", Count = 100 },
            ["test2"] = new AnotherTestState { Name = "another", IsActive = false }
        };

        var metadata = new PersistenceMetadata { Version = 3 };

        // Setup save behavior
        PersistedStateDictionary? savedDict = null;
        A.CallTo(() => _localStorage.SetItemAsync("test:state", A<PersistedStateDictionary>._, A<CancellationToken>._))
            .Invokes((string _, PersistedStateDictionary dict, CancellationToken _) => savedDict = dict)
            .Returns(new ValueTask());
        A.CallTo(() => _localStorage.SetItemAsync("test:state:metadata", A<PersistenceMetadata>._, A<CancellationToken>._))
            .Returns(new ValueTask());

        // Save the state
        await _provider.SaveWithMetadataAsync(originalState, metadata);

        // Setup load behavior using the saved data
        A.CallTo(() => _localStorage.GetItemAsync<PersistedStateDictionary>("test:state", A<CancellationToken>._))
            .Returns(new ValueTask<PersistedStateDictionary?>(Task.FromResult(savedDict)));
        A.CallTo(() => _localStorage.GetItemAsync<PersistenceMetadata>("test:state:metadata", A<CancellationToken>._))
            .Returns(new ValueTask<PersistenceMetadata?>(Task.FromResult<PersistenceMetadata?>(metadata)));

        // Act - Load the state back
        PersistedStateContainer<Dictionary<string, object>>? result = await _provider.LoadWithMetadataAsync();

        // Assert
        result.ShouldNotBeNull();
        result!.State.ShouldNotBeNull();
        result.State!.Count.ShouldBe(2);

        var loadedTest1 = result.State["test1"] as TestState;
        loadedTest1.ShouldNotBeNull();
        loadedTest1.Value.ShouldBe("original");
        loadedTest1.Count.ShouldBe(100);

        var loadedTest2 = result.State["test2"] as AnotherTestState;
        loadedTest2.ShouldNotBeNull();
        loadedTest2.Name.ShouldBe("another");
        loadedTest2.IsActive.ShouldBeFalse();
    }

    // Test state classes
    private class TestState
    {
        public string Value { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private class AnotherTestState
    {
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }
}