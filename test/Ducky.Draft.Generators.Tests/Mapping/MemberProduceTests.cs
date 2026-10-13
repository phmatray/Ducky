using System.Reflection;
using Ducky.Draft.Generators.Tests.Infrastructure;

namespace Ducky.Draft.Generators.Tests.Mapping;

// INV-26 on generated collection, nested and nullable members (SPEC §13.3): members are tri-state (untouched, drafted,
// assigned), the no-op identity check is generated per member kind, and every draft returns its own source when clean.
// Each scenario runs as compiled C# from this run of the generator (GeneratorHarness.Execute) and returns its
// observations, which the test asserts.
public sealed class MemberProduceTests
{
    private const string Source = """
        #pragma warning disable CS1591 // justification: undocumented probe source; only generated code is checked
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        namespace App;
        [Ducky.Draft.Draftable] public sealed partial record Todo(int Id, string Title);
        [Ducky.Draft.Draftable]
        public sealed partial record Board(ImmutableList<Todo> Items, ImmutableArray<Todo> Cards, ImmutableArray<int> Numbers, Todo Pinned)
        {
            public ImmutableDictionary<string, Todo> ById { get; init; } = ImmutableDictionary<string, Todo>.Empty;
            public ImmutableDictionary<string, int> Counts { get; init; } = ImmutableDictionary<string, int>.Empty;
            public ImmutableHashSet<string> Tags { get; init; } = [];
            public ImmutableSortedSet<int> Ranks { get; init; } = [];
            public ImmutableList<string> Names { get; init; } = [];
            public ImmutableList<Todo?> Holes { get; init; } = [];
            public Todo? Selected { get; init; }
            public ImmutableList<Todo>? MaybeItems { get; init; }
            public ImmutableArray<int>? MaybeNumbers { get; init; }
            public ImmutableDictionary<string, Todo>? MaybeById { get; init; }
            public ImmutableHashSet<string>? MaybeTags { get; init; }
        }
        #nullable disable
        [Ducky.Draft.Draftable] public sealed partial record Legacy(Todo Child, ImmutableList<Todo> Items, ImmutableArray<Todo> Cards);
        #nullable restore
        public static class Probe
        {
            private static readonly Todo A = new(1, "a"), B = new(2, "b"), C = new(3, "c");

            private static Board Sample() => new([A, B, C], [A, B, C], [1, 2, 3], A)
            {
                ById = ImmutableDictionary<string, Todo>.Empty.Add("a", A).Add("b", B),
                Counts = ImmutableDictionary<string, int>.Empty.Add("k", 1),
                Tags = ["t"],
                Ranks = [2, 1],
                Names = ["n"],
                Holes = [A, null],
            };

            private static string Throws(System.Action action)
            {
                try { action(); return "none"; }
                catch (System.Exception e) { return e.GetType().Name; }
            }

            public static object?[] NoOpArray()
            {
                var b = Sample();
                var r1 = b.Produce(d => { });
                var r2 = b.Produce(d => { _ = d.Numbers.Count; _ = d.Cards[0].Title; _ = d.Items[1].Id; });
                var r3 = b.Produce(d => d.Numbers = b.Numbers);
                var r4 = b.Produce(d => d.Numbers[0] = d.Numbers[0]);
                var r5 = b.Produce(d => d.Pinned.Title = "changed");
                var r6 = b.Produce(d => d.Numbers = b.Numbers.Add(4).RemoveAt(3));
                return [ReferenceEquals(r1, b), ReferenceEquals(r2, b), ReferenceEquals(r3, b), ReferenceEquals(r4, b), ReferenceEquals(r5, b),
                    r5.Numbers == b.Numbers, r5.Cards == b.Cards, ReferenceEquals(r5.Items, b.Items), ReferenceEquals(r6, b)];
            }

            public static object?[] DefaultArray()
            {
                var b = Sample() with { Numbers = default, Cards = default };
                int numbers = -1, cards = -1;
                var r1 = b.Produce(d => { numbers = d.Numbers.Count; cards = d.Cards.Count; });
                var r2 = b.Produce(d => d.Pinned.Title = "x");
                var r3 = b.Produce(d => { d.Numbers.Add(5); d.Cards.Add(new Todo(9, "nine")); });
                return [numbers, cards, ReferenceEquals(r1, b), r1.Numbers.IsDefault, r2.Numbers.IsDefault, r2.Cards.IsDefault,
                    string.Join(",", r3.Numbers), r3.Cards.Single().Title];
            }

            public static object?[] NullableNull()
            {
                var b = Sample();
                var reads = new List<object?>();
                var r1 = b.Produce(d => reads.AddRange([d.Selected, d.MaybeItems, d.MaybeNumbers, d.MaybeById, d.MaybeTags]));
                var legacy = new Legacy(null!, null!, default);
                var r2 = legacy.Produce(d => reads.AddRange([d.Child, d.Items]));
                var list = ImmutableList.Create(new Todo(5, "five"));
                ImmutableArray<int> array = [7, 8];
                var r3 = b.Produce(d => { d.MaybeItems = new(list, static x => new Todo.Draft(x)); d.MaybeNumbers = array; d.Selected = B; });
                var cleared = false;
                var r4 = r3.Produce(d =>
                {
                    d.Selected = null; d.MaybeItems = null; d.MaybeNumbers = null; d.MaybeTags = null;
                    cleared = d.Selected is null && d.MaybeItems is null && d.MaybeNumbers is null;
                });
                var r5 = legacy.Produce(d => d.Items = new(list, static x => new Todo.Draft(x)));
                var r6 = legacy.Produce(d => d.Child = A);
                var tagged = b with { MaybeTags = ["m"] };
                var r7 = tagged.Produce(d => d.MaybeTags = r3.MaybeTags);
                var r8 = b.Produce(d => d.MaybeTags = r3.MaybeTags);
                return [reads.Count, reads.All(r => r is null), ReferenceEquals(r1, b), ReferenceEquals(r2, legacy),
                    ReferenceEquals(r3.MaybeItems, list), r3.MaybeNumbers == array, ReferenceEquals(r3.Selected, B),
                    cleared, r4.Selected is null && r4.MaybeItems is null && r4.MaybeNumbers is null && r4.MaybeTags is null,
                    ReferenceEquals(r5.Items, list), ReferenceEquals(r6.Child, A), r6.Items is null,
                    r7.MaybeTags is null, ReferenceEquals(r8, b)];
            }

            public static object?[] StructuralOps()
            {
                var b = Sample();
                string afterRemove = "";
                int nextId = 0;
                var r1 = b.Produce(d =>
                {
                    d.Items[0].Title = "x";
                    d.Items.RemoveAt(1);
                    afterRemove = d.Items[0].Title;
                    nextId = d.Items[1].Id;
                    d.Cards[2].Title = "z";
                    d.Cards.Insert(0, new Todo(0, "zero"));
                    d.ById["b"].Title = "bee";
                });
                var r2 = b.Produce(d => d.Items[0].Title = "y");
                return [afterRemove, nextId, string.Join(",", r1.Items.Select(t => t.Title)), ReferenceEquals(r1.Items[1], C),
                    string.Join(",", r1.Cards.Select(t => t.Title)), ReferenceEquals(r1.Cards[1], A), r1.ById["b"].Title, ReferenceEquals(r1.ById["a"], A),
                    r2.Items[0].Title, ReferenceEquals(r2.Items[1], B), r2.Cards == b.Cards];
            }

            public static object?[] AssignRecord()
            {
                var b = Sample();
                var other = new Todo(7, "other");
                var r1 = b.Produce(d => d.Pinned = other);
                var r2 = b.Produce(d => d.Selected = other);
                var r3 = b.Produce(d => { d.Pinned = other; d.Pinned.Title = "edited"; });
                var r4 = b.Produce(d => d.Pinned = b.Pinned);
                var r5 = b.Produce(d => d.Items = new(ImmutableList.Create(other), static x => new Todo.Draft(x)));
                var r6 = b.Produce(d => d.Selected = null);
                return [ReferenceEquals(r1.Pinned, other), ReferenceEquals(r2.Selected, other), r3.Pinned.Title, other.Title,
                    ReferenceEquals(r4, b), ReferenceEquals(r5.Items[0], other), ReferenceEquals(r6, b)];
            }

            public static object?[] OtherKinds()
            {
                var b = Sample();
                var r = b.Produce(d =>
                {
                    d.Tags.Add("u");
                    d.Ranks.Add(0);
                    d.Counts["k"] = 2;
                    d.Names.Add("m");
                    d.Holes[0] = null;
                    d.Holes.Add(B);
                    d.MaybeTags = ImmutableHashSet.Create("w");
                    d.MaybeById = new(b.ById, static x => new Todo.Draft(x));
                });
                var untouched = b.Produce(d => { d.Counts["k"] = 1; d.Tags.Remove("missing"); _ = d.Holes[1]; _ = d.ById["a"].Title; });
                return [string.Join(",", r.Tags.OrderBy(t => t)), string.Join(",", r.Ranks), r.Counts["k"], string.Join(",", r.Names),
                    r.Holes[0] is null, ReferenceEquals(r.Holes[2], B), string.Join(",", r.MaybeTags!), ReferenceEquals(r.MaybeById, b.ById),
                    ReferenceEquals(untouched, b)];
            }

            public static object?[] IsDirty()
            {
                var b = Sample();
                var d = b.CreateDraft();
                var dirty = (Ducky.Draft.IDraft<Board>)d;
                var states = new List<bool> { dirty.IsDirty };
                _ = d.Items[0].Title;
                _ = d.Pinned.Id;
                _ = d.Selected;
                states.Add(dirty.IsDirty);
                d.Pinned.Title = "p";
                states.Add(dirty.IsDirty);
                d.Pinned.Title = A.Title;
                states.Add(dirty.IsDirty);
                d.Items[0].Title = "i";
                states.Add(dirty.IsDirty);
                var e = b.CreateDraft();
                e.Tags = b.Tags;
                states.Add(((Ducky.Draft.IDraft<Board>)e).IsDirty);
                states.Add(ReferenceEquals(e.Build(), b));
                return [.. states.Select(s => (object?)s)];
            }

            public static object?[] Revocation()
            {
                var b = Sample();
                Todo.Draft? pinned = null;
                Ducky.Draft.ListDraft<Todo, Todo.Draft>? items = null;
                Ducky.Draft.ListDraft<int>? numbers = null;
                b.Produce(d => { pinned = d.Pinned; items = d.Items; numbers = d.Numbers; _ = d.Selected; });
                string replacedChild = "", replacedList = "", sameChild = "";
                b.Produce(d =>
                {
                    var child = d.Pinned;
                    d.Pinned = new Todo(8, "eight");
                    replacedChild = Throws(() => child.Title = "x");
                    var list = d.Names;
                    d.Names = ImmutableList.Create("z");
                    replacedList = Throws(() => list.Add("x"));
                    var same = d.Items;
                    d.Items = same;
                    sameChild = Throws(() => same[0].Title = "kept");
                });
                return [Throws(() => pinned!.Title = "x"), Throws(() => _ = items!.Count), Throws(() => _ = numbers!.Count),
                    replacedChild, replacedList, sameChild];
            }

            // SPEC §13.3 / INV-26: a setter revokes the draft it replaces (unless it is the same instance), so a write through
            // it throws. That holds after earlier assignments too (a converted record, a converted collection).
            public static object?[] ReplacedAssigned()
            {
                var b = Sample();
                var child = Throws(() => b.Produce(d =>
                {
                    d.Pinned = new Todo(7, "seven");
                    var c = d.Pinned;
                    d.Pinned = new Todo(8, "eight");
                    c.Title = "lost";
                }));
                var list = Throws(() => b.Produce(d =>
                {
                    d.Names = ImmutableList.Create("z");
                    var l = d.Names;
                    d.Names = ImmutableList.Create("y");
                    l.Add("lost");
                }));
                var kept = b.Produce(d => { d.Pinned = new Todo(7, "seven"); d.Pinned = d.Pinned; d.Pinned.Title = "kept"; });
                return [child, list, kept.Pinned.Title];
            }

            // Aliasing one draft into two places (two members, or a member and a collection element) is unsupported: the
            // holders share mutations, so no `with` script matches it (INV-26). SPEC §13.3 revokes a replaced draft unless it
            // is the same instance, so replacing either holder revokes the shared draft and the next use of the other holder
            // (a write, a read, or Build) throws instead of building a result that diverges from `with`.
            public static object?[] ReplacedAliased()
            {
                var b = Sample();
                return
                [
                    Throws(() => b.Produce(d => { d.Selected = d.Pinned; d.Pinned = new Todo(9, "nine"); d.Selected!.Title = "p"; })),
                    Throws(() => b.Produce(d => { d.MaybeItems = d.Items; d.Items = new(ImmutableList<Todo>.Empty, static x => new Todo.Draft(x)); d.MaybeItems!.Add(C); })),
                    Throws(() => b.Produce(d => { d.Selected = d.Items[0]; d.Selected = null; d.Items[0].Title = "y"; })),
                    Throws(() => b.Produce(d => { d.Selected = d.Pinned; d.Selected = null; d.Pinned.Title = "alias"; })),
                    b.Produce(d => { d.Selected = d.Pinned; d.Selected = d.Pinned; d.Pinned.Title = "kept"; }).Selected!.Title,
                ];
            }
        }
        """;

    private static object?[] Run(string scenario)
    {
        object?[] result = [];
        GeneratorHarness.Run(new DraftGenerator(), GeneratorHarness.Compile(Source), out var output);
        GeneratorHarness.Execute(output, assembly => result = (object?[])assembly.GetType("App.Probe")!.GetMethod(scenario)!.Invoke(null, null)!);
        return result;
    }

    // A no-op recipe on a record with ImmutableArray<T> members returns the base: their identity is the underlying array
    // (ImmutableArray<T>.Equals), reading drafts and setting an equal value is no change, and an untouched array keeps its array.
    [Fact]
    public void Produce_NoOp_ImmutableArrayMember_ReturnsSameReference() =>
        Run("NoOpArray").ShouldBe([true, true, true, true, false, true, true, true, false]);

    // A default (uninitialized) ImmutableArray<T> member reads as empty through the draft and stays default when untouched.
    [Fact]
    public void Produce_DefaultImmutableArrayMember_Works() =>
        Run("DefaultArray").ShouldBe([0, 0, true, true, true, true, "5", "nine"]);

    // Getters of nullable members (annotated, a nullable ImmutableArray<T>, oblivious under #nullable disable) return null
    // while the original is null and nothing was assigned; assigning, null included, is reflected by Build: a null
    // collection assigned through the implicit conversion sets the member to null.
    [Fact]
    public void Produce_NullableMemberNullOriginal_GetReturnsNull() =>
        Run("NullableNull").ShouldBe([7, true, true, true, true, true, true, true, true, true, true, true, true, true]);

    // A structural list operation flushes the cached element drafts first: earlier element edits survive index shifts.
    [Fact]
    public void Produce_ListStructuralOps_FlushElementDrafts() =>
        Run("StructuralOps").ShouldBe(["x", 3, "x,c", true, "zero,a,b,z", true, "bee", true, "y", true, true]);

    // `d.Child = record` compiles through the generated implicit conversion and builds that record when clean.
    [Fact]
    public void Produce_AssignRecordToDraftableMember_Builds() =>
        Run("AssignRecord").ShouldBe([true, true, "edited", "other", true, true, true]);

    [Fact]
    public void Produce_SetDictionaryAndPlainListMembers_Build() =>
        Run("OtherKinds").ShouldBe(["t,u", "0,1,2", 2, "n,m", true, true, "w", true, true]);

    // IsDirty is conservative: an assigned member counts, a drafted member counts while its draft is dirty.
    [Fact]
    public void Draft_DraftedMembers_IsDirty() =>
        Run("IsDirty").ShouldBe([false, false, true, false, true, true, true]);

    // Revoking a draft revokes its member drafts, and an assignment revokes the draft it replaces (not the same instance).
    [Fact]
    public void Draft_MemberDrafts_Revoked() =>
        Run("Revocation").ShouldBe(["ObjectDisposedException", "ObjectDisposedException", "ObjectDisposedException", "ObjectDisposedException", "ObjectDisposedException", "none"]);

    // An assignment revokes the draft it replaces even when the member was assigned before (SPEC §13.3, INV-26).
    [Fact]
    public void Draft_ReplacedAssignedDraft_Revoked() =>
        Run("ReplacedAssigned").ShouldBe(["ObjectDisposedException", "ObjectDisposedException", "kept"]);

    // A replaced draft is revoked even when another member or a collection element still holds it (aliasing is unsupported
    // and fails loudly, SPEC §13.3): the other holder then throws; reassigning the same instance keeps it.
    [Fact]
    public void Draft_ReplacedAliasedDraft_Revoked() =>
        Run("ReplacedAliased").ShouldBe(["ObjectDisposedException", "ObjectDisposedException", "ObjectDisposedException", "ObjectDisposedException", "kept"]);
}
