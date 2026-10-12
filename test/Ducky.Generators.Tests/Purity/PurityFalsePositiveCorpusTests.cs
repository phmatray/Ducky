using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// The false-positive corpus (SPEC §16.2): every pure reducer, mutator and projector shape of the existing snippets (SPEC.md and
// the Ducky.Tests, Ducky.Blazor and Ducky.Testing fixtures) produces zero DUCKY001–003 hits. Samples (§24 step 18) and
// the docs snippets join it through their own warnings-as-errors builds, which run this analyzer.
public sealed class PurityFalsePositiveCorpusTests
{
    private const string Corpus = """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        using System.Text;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record Item(int Id, string Name);
        public sealed record Cart(ImmutableList<Item> Items, int Count, string? Error, bool Initialized);
        public sealed record Journal(ImmutableArray<string> Steps);
        public sealed record Counter(int Value);
        public sealed record Dial(int Value);

        public sealed record LoggedOut;
        public sealed record AddItem(Item Item);
        public sealed record RemoveItem(int Id);
        public sealed record ProductsLoaded(int Count);
        public sealed record ProductsFailed(string Error);
        public sealed record Mark(string Name);
        public sealed record SetDial(int Value);
        public sealed record Increment;
        public sealed record Rename(int Id, string Name);
        public sealed record Load;

        public sealed class CartSlice : Slice<Cart>
        {
            private const int Limit = 10;

            protected override Cart Initial => new([], 0, null, false);

            public CartSlice()
            {
                On<LoggedOut>(_ => Initial);
                On<AddItem>((s, a) => s with { Items = s.Items.Add(a.Item), Count = s.Count + 1 });
                On<RemoveItem>((s, a) => s with { Items = s.Items.RemoveAll(x => x.Id == a.Id) });
                On<ProductsLoaded>((state, action) => state with { Count = action.Count, Error = null });
                On<ProductsFailed>((state, action) => state with { Error = action.Error });
                On<StoreInitialized>(state => state with { Initialized = true });
                On<Increment>(static state => state.Count >= Limit ? state : state with { Count = state.Count + 1 });
                On<Rename>((s, a) =>
                {
                    var index = s.Items.FindIndex(x => x.Id == a.Id);
                    if (index < 0)
                    {
                        return s;
                    }

                    var names = new List<string>();
                    foreach (var item in s.Items)
                    {
                        names.Add(item.Name);
                    }

                    var builder = new StringBuilder();
                    builder.Append(string.Join(",", names));
                    var total = 0;
                    for (var i = 0; i < s.Items.Count; i++)
                    {
                        total += s.Items[i].Id;
                    }

                    return s with
                    {
                        Items = s.Items.SetItem(index, s.Items[index] with { Name = a.Name }),
                        Error = builder.Length > total ? builder.ToString() : null,
                    };
                });
            }
        }

        public sealed class JournalSlice : Slice<Journal>
        {
            protected override Journal Initial => new([]);

            public JournalSlice()
            {
                On<Mark>((state, mark) => new([.. state.Steps, mark.Name]));
                On<Load>(state => state.Steps.Length switch
                {
                    0 => state,
                    var n when n > 100 => throw new InvalidOperationException("too many steps"),
                    _ => new(state.Steps.RemoveAt(0)),
                });
            }
        }

        public sealed class DialSlice : Slice<Dial>
        {
            protected override Dial Initial => new(0);

            public DialSlice() => On<SetDial>(static (_, action) => new(Math.Clamp(action.Value, 0, 10)));
        }

        public sealed class CartStore : SliceStore<Cart>
        {
            protected override Cart Initial => new([], 0, null, false);

            public CartStore() => On<LoggedOut>(_ => Initial);

            public int Count => State.Items.Count;

            public void Add(Item i) => Set(s => s with { Items = s.Items.Add(i), Count = s.Count + 1 });

            public void Remove(int id) => Set(s => s with { Items = s.Items.RemoveAll(x => x.Id == id) }, "Drop");

            public Task<DispatchResult> ClearAsync() => SetAsync(_ => Initial);
        }

        public sealed class Loader : EffectGroup
        {
            public Loader() => On<Load>((_, ctx, _) => Task.Delay(TimeSpan.FromSeconds(1), ctx.Time));
        }

        public static class Selectors
        {
            public static readonly SelectorDef<int> Doubled = Selector.Define(s => s.Get<Counter>().Value, c => c * 2);
            public static readonly SelectorDef<int, int> Plus = Selector.Define(s => s.Get<Counter>().Value, (int c, int arg) => c + arg);
            public static readonly SelectorDef<string> Names = Selector.Define(
                s => s.Get<Cart>(),
                s => s.Get<Journal>(),
                (cart, journal) => string.Join(", ", cart.Items.Where(i => i.Id > 0).Select(i => i.Name).Concat(journal.Steps).OrderBy(n => n)));
            public static readonly SelectorDef<ImmutableDictionary<int, Item>> ById = Selector.Define(
                s => s.Get<Cart>().Items,
                items => items.ToImmutableDictionary(i => i.Id));
            public static readonly SelectorDef<int> Last = Selector.Define(
                s => s.Get<Cart>().Items,
                items =>
                {
                    var copy = items.ToList();
                    copy.Sort((x, y) => x.Id.CompareTo(y.Id));
                    return copy.Count == 0 ? 0 : copy[^1].Id;
                });
        }
        """;

    [Fact]
    public Task Purity_FalsePositiveCorpus_ZeroHits() => Verifier.VerifyAsync(Corpus);
}
