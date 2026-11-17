using Ducky.Testing;

namespace Ducky.Docs.Tests.Core;

#region union-effects-actions
public sealed record AddNote(string Text);

public sealed record RemoveNote(string Text);

public sealed record ClearNotes;

// [DuckyAction] on the union generates the helpers of each case: AddNote(...), RemoveNote(...), ClearNotes().
[DuckyAction]
public union NoteAction(AddNote, RemoveNote, ClearNotes);
#endregion

public sealed record NoteLogged(string Line);

// The snippets of docs/guides/core/union-effects.md.
public sealed partial class UnionEffectsGuideTests
{
    // A private class, as in the effects guide: AddDuckyGenerated_Ducky_Docs_Tests registers every accessible effect of
    // this assembly. A union effect group and the types around it are partial, for its generated RegisterCases.
    private static partial class App
    {
        #region union-effects-group
        public sealed partial class NoteEffects : EffectGroup<NoteAction>
        {
            // One handler for every case; the compiler checks the switch covers them all (CS8509).
            public override Task Handle(NoteAction action, EffectContext context, CancellationToken cancellationToken)
            {
                context.Dispatch(new NoteLogged(action switch
                {
                    AddNote a => "added " + a.Text,
                    RemoveNote r => "removed " + r.Text,
                    ClearNotes => "cleared",
                }));
                return Task.CompletedTask;
            }
        }
        #endregion
    }

    // #314 acceptance 1 and 2: the generated helpers dispatch each case, and the generated registration runs the group's
    // Handle for each one.
    [Fact]
    public async Task UnionEffects_HelpersDispatchEachCase_GroupHandlesEach()
    {
        #region union-effects-test
        await using var store = TestStore.Create(d =>
        {
            d.ThrowOnUnhandledAction = false; // NoteLogged has no reducer here
            d.AddEffect<App.NoteEffects>();
        });

        store.AddNote("milk");
        store.RemoveNote("milk");
        store.ClearNotes();
        await store.Settled();

        store.Processed.Select(p => p.Action).OfType<NoteLogged>()
            .ShouldBe([new NoteLogged("added milk"), new NoteLogged("removed milk"), new NoteLogged("cleared")]);
        #endregion
    }
}
