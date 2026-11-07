using System.Collections.Immutable;
using Ducky.Draft;
using Ducky.Testing;

namespace Ducky.Docs.Tests.Draft;

#region draft-state
[Draftable]
public sealed partial record Board(ImmutableList<Card> Cards, string Filter);

[Draftable]
public sealed partial record Card(int Id, string Title, bool Done);
#endregion

public sealed record CardAdded(int Id, string Title);

public sealed record CardCompleted(int Id);

public sealed record DoneCardsCleared;

public sealed record FilterChanged(string Filter);

#region draft-slice
public sealed class BoardSlice : Slice<Board>
{
    public BoardSlice()
    {
        On<CardAdded>((state, action) => state.Produce(draft => draft.Cards.Add(new Card(action.Id, action.Title, Done: false))));
        On<CardCompleted>((state, action) => state.Produce(draft =>
        {
            if (draft.Cards.FirstOrDefault(card => card.Id == action.Id) is { } card)
            {
                card.Done = true;
            }
        }));
        On<DoneCardsCleared>(state => state.Produce(draft => draft.Cards.RemoveAll(card => card.Done)));
        On<FilterChanged>((state, action) => state.Produce(draft => draft.Filter = action.Filter));
    }

    protected override Board Initial => new([], Filter: "all");
}
#endregion

// The snippets of docs/guides/draft/produce.md.
public sealed class DraftGuideTests
{
    private static readonly Board _seeded = new([new Card(1, "Write docs", Done: false), new Card(2, "Ship", Done: true)], Filter: "all");

    // Non-normative (M15-03): each reducer edits its draft; untouched cards keep their references.
    [Fact]
    public void Produce_EditsDraft_UntouchedCardsKeepReferences()
    {
        SliceTest.For<BoardSlice, Board>().Given(_seeded).When(new CardAdded(3, "Celebrate"))
            .Then(state => state.Cards.Select(static c => c.Title).ShouldBe(["Write docs", "Ship", "Celebrate"]));
        SliceTest.For<BoardSlice, Board>().Given(_seeded).When(new CardCompleted(1))
            .Then(state =>
            {
                state.Cards[0].ShouldBe(new Card(1, "Write docs", Done: true));
                state.Cards[1].ShouldBeSameAs(_seeded.Cards[1]);
            });
        SliceTest.For<BoardSlice, Board>().Given(_seeded).When(new DoneCardsCleared())
            .Then(state => state.Cards.ShouldBe([_seeded.Cards[0]]));
        SliceTest.For<BoardSlice, Board>().Given(_seeded).When(new FilterChanged("open"))
            .Then(state => (state.Filter, state.Cards).ShouldBe(("open", _seeded.Cards)));
    }

    // Non-normative (M15-03): a recipe that changes nothing returns the same instance, so subscribers are not notified.
    [Theory]
    [InlineData(2)] // already done
    [InlineData(9)] // no such card
    public void Produce_NothingChanged_SameReference(int id) =>
        SliceTest.For<BoardSlice, Board>().Given(_seeded).When(new CardCompleted(id)).ThenUnchanged();

    // Non-normative (M15-03): a RemoveAll that matches nothing is no change, so the reducer notifies nobody.
    [Fact]
    public void Produce_RemoveAllMatchingNothing_SameReference() =>
        SliceTest.For<BoardSlice, Board>().Given(_seeded with { Cards = [_seeded.Cards[0]] }).When(new DoneCardsCleared()).ThenUnchanged();

    // Non-normative (M15-03): Add then RemoveAt builds a new list with equal contents; Clear of an empty list is no change.
    [Fact]
    public void Produce_CollectionOperations_ChangeOnlyWhenTheyWrite()
    {
        var readded = _seeded.Produce(draft =>
        {
            draft.Cards.Add(new Card(3, "Test", Done: false));
            draft.Cards.RemoveAt(2);
        });
        var empty = _seeded with { Cards = [] };

        readded.Cards.ShouldNotBeSameAs(_seeded.Cards);
        readded.Cards.ShouldBe(_seeded.Cards);
        empty.Produce(draft => draft.Cards.Clear()).ShouldBeSameAs(empty);
    }

    // Non-normative (M15-03): a value set back to its original is no change either.
    [Fact]
    public void Produce_Reverted_SameReference()
    {
        #region draft-revert
        var same = _seeded.Produce(draft =>
        {
            draft.Filter = "open";
            draft.Filter = "all"; // back to the original value
        });
        #endregion

        same.ShouldBeSameAs(_seeded);
    }

    // Non-normative (M15-03): CreateDraft edits in several steps; Build returns the result.
    [Fact]
    public void CreateDraft_SeveralSteps_Builds()
    {
        #region draft-create
        var draft = _seeded.CreateDraft();
        draft.Filter = "done";
        draft.Cards.RemoveAll(card => !card.Done);
        var board = draft.Build();
        #endregion

        board.ShouldBe(_seeded with { Cards = [_seeded.Cards[1]], Filter = "done" }, new BoardComparer());
    }

    // Non-normative (M15-03): a draft kept past Produce is revoked: writing through it throws (DUCKY106 flags the escape).
    [Fact]
    public void Draft_UsedAfterProduce_Throws()
    {
        Board.Draft? kept = null;
#pragma warning disable DUCKY106 // justification: the escape DUCKY106 reports at build time is what this test runs
        _ = _seeded.Produce(draft => kept = draft);
#pragma warning restore DUCKY106

        Should.Throw<ObjectDisposedException>(() => kept!.Filter = "open");
    }

    private sealed class BoardComparer : IEqualityComparer<Board>
    {
        public bool Equals(Board? x, Board? y) => x!.Filter == y!.Filter && x.Cards.SequenceEqual(y.Cards);

        public int GetHashCode(Board obj) => obj.Filter.GetHashCode(StringComparison.Ordinal);
    }
}
