using R3dux.Exceptions;
using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

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
        act.Should().Throw<R3duxException>()
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