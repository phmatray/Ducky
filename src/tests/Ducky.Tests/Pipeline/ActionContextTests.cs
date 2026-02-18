using Ducky.Pipeline;

namespace Ducky.Tests.Pipeline;

public class ActionContextTests
{
    [Fact]
    public void Constructor_WithValidAction_ShouldInitializeProperties()
    {
        // Arrange
        var action = new TestAction { Value = 42 };

        // Act
        var context = new ActionContext(action);

        // Assert
        context.Action.ShouldBe(action);
        context.IsAborted.ShouldBeFalse();
        context.Metadata.ShouldNotBeNull();
        context.Metadata.ShouldBeEmpty();
        context.StateProvider.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ActionContext(null!))
            .ParamName
            .ShouldBe("action");
    }

    [Fact]
    public void Abort_ShouldSetIsAbortedToTrue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act
        context.Abort();

        // Assert
        context.IsAborted.ShouldBeTrue();
    }

    [Fact]
    public void Abort_WhenCalledMultipleTimes_ShouldRemainAborted()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act
        context.Abort();
        context.Abort();
        context.Abort();

        // Assert
        context.IsAborted.ShouldBeTrue();
    }

    [Fact]
    public void SetMetadata_ShouldAddOrUpdateValue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act
        context.SetMetadata("key1", "value1");
        context.SetMetadata("key2", 42);

        // Assert
        context.Metadata["key1"].ShouldBe("value1");
        context.Metadata["key2"].ShouldBe(42);
    }

    [Fact]
    public void SetMetadata_WithExistingKey_ShouldUpdateValue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        context.SetMetadata("key", "old value");

        // Act
        context.SetMetadata("key", "new value");

        // Assert
        context.Metadata["key"].ShouldBe("new value");
    }

    [Fact]
    public void TryGetMetadata_WithExistingKey_ShouldReturnTrueAndValue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        context.SetMetadata("key", "value");

        // Act
        bool result = context.TryGetMetadata("key", out string? value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe("value");
    }

    [Fact]
    public void TryGetMetadata_WithNonExistingKey_ShouldReturnFalseAndDefault()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act
        bool result = context.TryGetMetadata("nonexistent", out string? value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryGetMetadata_WithWrongType_ShouldReturnTrueAndDefault()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        context.SetMetadata("key", "string value");

        // Act
        bool result = context.TryGetMetadata("key", out int value);

        // Assert
        result.ShouldBeTrue(); // Returns true because key exists
        value.ShouldBe(0); // But value is default because type doesn't match
    }

    [Fact]
    public void GetOrAddMetadata_WithExistingKey_ShouldReturnExistingValue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        context.SetMetadata("key", "existing");

        // Act
        string? result = context.GetOrAddMetadata("key", () => "new");

        // Assert
        result.ShouldBe("existing");
    }

    [Fact]
    public void GetOrAddMetadata_WithNonExistingKey_ShouldAddAndReturnNewValue()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        var factoryCalled = false;

        // Act
        string? result = context.GetOrAddMetadata(
            "key",
            () =>
            {
                factoryCalled = true;
                return "created";
            });

        // Assert
        result.ShouldBe("created");
        factoryCalled.ShouldBeTrue();
        context.Metadata["key"].ShouldBe("created");
    }

    [Fact]
    public void GetOrAddMetadata_WithNullFactory_ShouldThrowNullReferenceException()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act & Assert
        Should.Throw<NullReferenceException>(() => context.GetOrAddMetadata<string>("key", null!));
    }

    [Fact]
    public void StateProvider_ShouldBeSettableAndGettable()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        var stateProvider = new TestStateProvider();

        // Act
        context.StateProvider = stateProvider;

        // Assert
        context.StateProvider.ShouldBe(stateProvider);
    }

    [Fact]
    public void SetMetadata_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => context.SetMetadata(null!, "value"))
            .ParamName
            .ShouldBe("key");
    }

    [Fact]
    public void TryGetMetadata_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => context.TryGetMetadata<string>(null!, out _))
            .ParamName
            .ShouldBe("key");
    }

    [Fact]
    public void Metadata_WithComplexTypes_ShouldWorkCorrectly()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        var complexObject = new ComplexMetadata
        {
            Id = Guid.NewGuid(),
            Items = new List<string> { "a", "b", "c" }
        };

        // Act
        context.SetMetadata("complex", complexObject);
        bool retrieved = context.TryGetMetadata<ComplexMetadata>("complex", out ComplexMetadata? value);

        // Assert
        retrieved.ShouldBeTrue();
        value.ShouldBe(complexObject);
        ReferenceEquals(value!.Items, complexObject.Items).ShouldBeTrue();
    }

    [Fact]
    public void ActionContext_ShouldSupportMultipleMetadataTypes()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act
        context.SetMetadata("string", "text");
        context.SetMetadata("int", 42);
        context.SetMetadata("bool", true);
        context.SetMetadata("guid", Guid.Empty);

        // Assert
        context.Metadata.Count.ShouldBe(4);
        context.TryGetMetadata<string>("string", out string? stringValue).ShouldBeTrue();
        context.TryGetMetadata<int>("int", out int intValue).ShouldBeTrue();
        context.TryGetMetadata<bool>("bool", out bool boolValue).ShouldBeTrue();
        context.TryGetMetadata<Guid>("guid", out Guid guidValue).ShouldBeTrue();

        stringValue.ShouldBe("text");
        intValue.ShouldBe(42);
        boolValue.ShouldBeTrue();
        guidValue.ShouldBe(Guid.Empty);
    }

    private record TestAction
    {
        public int Value { get; init; }
    }

    private record TestState
    {
        public int Value { get; init; }
    }

    private class ComplexMetadata
    {
        public Guid Id { get; init; }
        public List<string> Items { get; init; } = new();
    }

#pragma warning disable RCS1079
    private class TestStateProvider : IStateProvider
    {
        public object State => new TestState { Value = 100 };

        public TState GetSlice<TState>() => throw new NotImplementedException();
        public TState GetSliceByKey<TState>(string key) => throw new NotImplementedException();

        public bool TryGetSlice<TState>(out TState? slice)
        {
            slice = default;
            return false;
        }

        public bool HasSlice<TState>() => false;
        public bool HasSliceByKey(string key) => false;
        public IReadOnlyCollection<string> GetSliceKeys() => [];
        public IReadOnlyDictionary<string, object> GetAllSlices() => new Dictionary<string, object>();

        public ImmutableSortedDictionary<string, object> GetStateDictionary()
            => ImmutableSortedDictionary<string, object>.Empty;

        public ImmutableSortedSet<string> GetKeys() => ImmutableSortedSet<string>.Empty;
    }
#pragma warning restore RCS1079
}