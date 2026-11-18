using System.Collections.Concurrent;
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

        public sealed record NotesOpened;

        public sealed record NotesFiltered(string Filter);

        public sealed record NotesLeft;

        public sealed record NotesLoaded(string Notes);

        public interface INoteSearch
        {
            Task<string> FindAsync(string filter, CancellationToken cancellationToken);
        }

        // In a class of its own, so the snippet can use the same effect name as the union effect that replaces it.
        public static class Before
        {
            #region union-effect-before
            // 1. An internal action that exists only to own the slot.
            public sealed record NotesLoadRequested(string Filter = "all", bool Abandon = false);

            // 2. A group whose every handler only dispatches that action again.
            public sealed class NotesTriggers : EffectGroup
            {
                public NotesTriggers()
                {
                    On<NotesOpened>((_, context, _) => Forward(context, new NotesLoadRequested()));
                    On<NotesFiltered>((action, context, _) => Forward(context, new NotesLoadRequested(action.Filter)));
                    On<NotesLeft>((_, context, _) => Forward(context, new NotesLoadRequested(Abandon: true)));
                }

                private static Task Forward(EffectContext context, NotesLoadRequested request)
                {
                    context.Dispatch(request);
                    return Task.CompletedTask;
                }
            }

            // 3. The effect that does the work.
            public sealed class LoadNotesEffect(INoteSearch search) : Effect<NotesLoadRequested>
            {
                public override Concurrency Policy => Concurrency.Switch;

                public override async Task Handle(NotesLoadRequested action, EffectContext context, CancellationToken cancellationToken)
                {
                    if (action.Abandon)
                    {
                        return;
                    }

                    context.Dispatch(new NotesLoaded(await search.FindAsync(action.Filter, cancellationToken)));
                }
            }
            #endregion
        }

        #region union-effect-slot
        public union LoadTrigger(NotesOpened, NotesFiltered, NotesLeft);

        public sealed partial class LoadNotesEffect(INoteSearch search) : Effect<LoadTrigger>
        {
            // One slot for every case of the union.
            public override Concurrency Policy => Concurrency.Switch;

            public override Task Handle(LoadTrigger trigger, EffectContext context, CancellationToken cancellationToken) =>
                trigger switch
                {
                    NotesOpened => Load("all", context, cancellationToken),
                    NotesFiltered filtered => Load(filtered.Filter, context, cancellationToken),
                    NotesLeft => Task.CompletedTask, // cancels the run in flight and starts nothing
                };

            private async Task Load(string filter, EffectContext context, CancellationToken cancellationToken) =>
                context.Dispatch(new NotesLoaded(await search.FindAsync(filter, cancellationToken)));
        }
        #endregion
    }

    // #345 acceptance 1: two different cases share one slot, so the newest case cancels the run of the other one.
    [Fact]
    public async Task UnionEffect_NewestCaseWins_OnlyItsResultIsProcessed()
    {
        var search = new GatedNoteSearch();
        await using var store = TestStore.Create(d =>
        {
            d.ThrowOnUnhandledAction = false; // the triggers and NotesLoaded have no reducer here
            d.AddEffect(new App.LoadNotesEffect(search));
        });

        await store.DispatchAsync(new App.NotesOpened());
        await search.Started("all");
        await store.DispatchAsync(new App.NotesFiltered("milk"));
        await search.Started("milk");

        search.Release("all");
        search.Release("milk");
        await store.Settled();

        store.Processed.Select(p => p.Action).OfType<App.NotesLoaded>().ShouldBe([new App.NotesLoaded("notes for milk")]);
    }

    // Holds each search until the test releases it, and honours the run's token as a real search does.
    private sealed class GatedNoteSearch : App.INoteSearch
    {
        private readonly ConcurrentDictionary<string, (TaskCompletionSource Started, TaskCompletionSource<string> Result)> _gates = new();

        public Task<string> FindAsync(string filter, CancellationToken cancellationToken)
        {
            var gate = GateOf(filter);
            gate.Started.TrySetResult();
            return gate.Result.Task.WaitAsync(cancellationToken);
        }

        public Task Started(string filter) =>
            GateOf(filter).Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        public void Release(string filter) => GateOf(filter).Result.TrySetResult("notes for " + filter);

        private (TaskCompletionSource Started, TaskCompletionSource<string> Result) GateOf(string filter) =>
            _gates.GetOrAdd(filter, _ => (
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)));
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
