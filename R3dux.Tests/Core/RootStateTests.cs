using R3dux.Exceptions;
using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class RootStateTests
{
    private const string TestKey = "testKey";
    private const string NonExistingKey = "nonExistingKey";
    
    private readonly RootState _sut = new();
    private readonly TestState _initialState = new() { Value = 42 };
    
    [Fact]
    public void AddOrUpdateSliceState_Should_Add_New_State()
    {
        // Act
        _sut.AddOrUpdateSliceState(TestKey, _initialState);

        // Assert
        _sut.ContainsKey(TestKey).Should().BeTrue();
        _sut.Select<TestState>(TestKey).Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void AddOrUpdateSliceState_Should_Update_Existing_State()
    {
        // Arrange
        var updatedState = new TestState { Value = 99 };

        _sut.AddOrUpdateSliceState(TestKey, _initialState);

        // Act
        _sut.AddOrUpdateSliceState(TestKey, updatedState);

        // Assert
        _sut.ContainsKey(TestKey).Should().BeTrue();
        _sut.Select<TestState>(TestKey).Should().BeEquivalentTo(updatedState);
    }

    [Fact]
    public void Indexer_Should_Get_And_Set_State()
    {
        // Act
        _sut[TestKey] = _initialState;
        var result = _sut[TestKey];

        // Assert
        result.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void Select_Should_Throw_Exception_If_State_Not_Found()
    {
        // Act
        Action act = () => _sut.Select<TestState>(NonExistingKey);

        // Assert
        act.Should().Throw<R3duxException>()
            .WithMessage($"State with key '{NonExistingKey}' is not of type 'TestState'.");
    }

    [Fact]
    public void ContainsKey_Should_Return_True_If_Key_Exists()
    {
        _sut.AddOrUpdateSliceState(TestKey, _initialState);

        // Act
        var result = _sut.ContainsKey(TestKey);

        // Assert
        result.Should().BeTrue();
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