using Ducky.Testing;

namespace DuckShop.Tests;

// The basket's reducers (tutorial steps 3 and 9).
public sealed class BasketTests
{
    #region test-basket
    [Fact]
    public void AddToBasket_SameDuckTwice_OneLineOfTwo() =>
        SliceTest.For<BasketSlice, BasketState>()
            .When(new AddToBasket("classic"), new AddToBasket("classic"))
            .Then(basket => basket.Lines.ShouldBe([new BasketLine("classic", 2)]));

    [Fact]
    public void RemoveFromBasket_LastOne_RemovesTheLine() =>
        SliceTest.For<BasketSlice, BasketState>()
            .Given(new BasketState([new BasketLine("pirate", 1)]))
            .When(new RemoveFromBasket("pirate"))
            .Then(basket => basket.Lines.ShouldBeEmpty());

    [Fact]
    public void RemoveFromBasket_DuckNotInBasket_SameState() =>
        SliceTest.For<BasketSlice, BasketState>()
            .Given(new BasketState([new BasketLine("pirate", 1)]))
            .When(new RemoveFromBasket("disco"))
            .ThenUnchanged();
    #endregion

    // Step 3: the order of the actions decides the lines' order, and each reducer call sees the
    // state the previous one returned.
    [Fact]
    public void AddToBasket_TwoDucks_LinesInOrderAdded() =>
        SliceTest.For<BasketSlice, BasketState>()
            .When(new AddToBasket("pirate"), new AddToBasket("classic"), new AddToBasket("pirate"))
            .Then(basket =>
            {
                basket.Lines.ShouldBe([new BasketLine("pirate", 2), new BasketLine("classic", 1)]);
                basket.Count.ShouldBe(3);
            });
}
