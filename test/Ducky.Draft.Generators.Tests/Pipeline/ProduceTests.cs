using System.Reflection;
using CsCheck;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Ducky.TestSupport;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// INV-26 on generated scalar members (SPEC §13.3): the no-op is decided at Build by comparing each member with the
// original, so a recipe whose final values equal the originals (reverts included) returns the base reference. The draft
// runs as compiled C# from this run of the generator (GeneratorHarness.Execute).
public sealed class ProduceTests
{
    private const string Source = """
        #pragma warning disable CS1591 // justification: undocumented probe source; only generated code is checked
        namespace App;
        public enum Color { Red, Green, Blue }
        [Ducky.Draft.Draftable]
        public sealed partial record Item(int Id, string Title, bool Done, Color Color, double? Score)
        {
            public int Length => Title.Length;
        }
        public static class Probe
        {
            public static object Make(object?[] v) => new Item((int)v[0]!, (string)v[1]!, (bool)v[2]!, (Color)(int)v[3]!, (double?)v[4]);
            public static object?[] Read(object item)
            {
                var i = (Item)item;
                return [i.Id, i.Title, i.Done, (int)i.Color, i.Score];
            }
            public static object Apply(object item, (int Member, object? Value)[] edits) => ((Item)item).Produce(d =>
            {
                foreach (var (member, value) in edits)
                {
                    switch (member)
                    {
                        case 0: d.Id = (int)value!; break;
                        case 1: d.Title = (string)value!; break;
                        case 2: d.Done = (bool)value!; break;
                        case 3: d.Color = (Color)(int)value!; break;
                        default: d.Score = (double?)value; break;
                    }
                }
            });
            public static object CreateDraft(object item) => ((Item)item).CreateDraft();
            public static object Keep(object item)
            {
                Item.Draft? kept = null;
                ((Item)item).Produce(d => kept = d);
                return kept!;
            }
            public static int Length(object draft) => ((Item.Draft)draft).Length;
            public static string Title(object draft) => ((Item.Draft)draft).Title;
            public static void SetTitle(object draft, string title) => ((Item.Draft)draft).Title = title;
            public static object Build(object draft) => ((Item.Draft)draft).Build();
            public static object? Convert(object? item)
            {
                Item.Draft? draft = (Item?)item;
                return draft;
            }
        }
        """;

    private const int Members = 5;

    private static readonly Gen<object?[]> _values = Gen.Select(Gen.Int, Gen.String, Gen.Bool, Gen.Int[0, 2], Gen.Double.Nullable())
        .Select(v => new object?[] { v.Item1, v.Item2, v.Item3, v.Item4, v.Item5 });

    private static readonly Gen<(int Member, object? Value)> _edit = Gen.Int[0, Members - 1].SelectMany(m => Value(m).Select(v => (m, v)));

    private static Gen<object?> Value(int member) => member switch
    {
        0 => Gen.Int.Select(v => (object?)v),
        1 => Gen.String.Select(v => (object?)v),
        2 => Gen.Bool.Select(v => (object?)v),
        3 => Gen.Int[0, 2].Select(v => (object?)v),
        _ => Gen.Double.Nullable().Select(v => (object?)v),
    };

    // An equal value, and a distinct instance for a string: equality, not identity, decides the scalar no-op.
    private static object? Copy(object? value) => value is string s ? new string(s.AsSpan()) : value;

    private static void WithProbe(Action<Probe> test)
    {
        GeneratorHarness.Run(new DraftGenerator(), GeneratorHarness.Compile(Source), out var output);
        GeneratorHarness.Execute(output, assembly => test(new Probe(assembly)));
    }

    // Random edits, then every touched member assigned an equal copy of its original value (SPEC §17.7: scalar reverts
    // included).
    [Fact]
    public void Produce_NoOpRecipe_ReturnsSameReference() => WithProbe(probe =>
        Property.Check(Gen.Select(_values, _edit.Array[0, 8]), input =>
        {
            var (values, edits) = input;
            var item = probe.Make(values);
            var reverts = edits.Select(e => e.Member).Distinct().Select(m => (m, Copy(values[m])));

            probe.Apply(item, [.. edits, .. reverts]).ShouldBeSameAs(item);
        }));

    // Any script: the result holds the last value written to each member, and is the base exactly when they all equal it.
    [Fact]
    public void Produce_RandomScalarEdits_BuildsLastValues() => WithProbe(probe =>
        Property.Check(Gen.Select(_values, _edit.Array[0, 8]), input =>
        {
            var (values, edits) = input;
            var item = probe.Make(values);
            var expected = (object?[])values.Clone();
            foreach (var (member, value) in edits)
            {
                expected[member] = value;
            }

            var result = probe.Apply(item, edits);

            probe.Read(result).ShouldBe(expected);
            ReferenceEquals(result, item).ShouldBe(expected.SequenceEqual(values));
            probe.Read(item).ShouldBe(values);
        }));

    [Fact]
    public void Produce_RevertedScalar_ReturnsSameReference() => WithProbe(probe =>
    {
        object?[] values = [1, "a", false, 0, 2.5];
        var item = probe.Make(values);

        probe.Apply(item, [(0, 2), (1, "b"), (4, null), (0, 1), (1, new string("a".AsSpan())), (4, 2.5)]).ShouldBeSameAs(item);
        probe.Apply(item, [(2, true), (2, false), (3, 2), (3, 0)]).ShouldBeSameAs(item);
        probe.Read(probe.Apply(item, [(1, "b"), (1, "c")])).ShouldBe([1, "c", false, 0, 2.5]);

        var draft = probe.CreateDraft(item);
        probe.SetTitle(draft, "b");
        probe.SetTitle(draft, "a");
        probe.Build(draft).ShouldBeSameAs(item);
    });

    [Fact]
    public void Draft_IsDirty_ComparesWithOriginal() => WithProbe(probe =>
    {
        var item = probe.Make([1, "a", false, 0, null]);
        var draft = (IDraft<object>)probe.CreateDraft(item);

        draft.IsDirty.ShouldBeFalse();
        probe.SetTitle(draft, "b");
        draft.IsDirty.ShouldBeTrue();
        probe.Read(draft.Build()).ShouldBe([1, "b", false, 0, null]);
        probe.SetTitle(draft, "a");
        draft.IsDirty.ShouldBeFalse();
        draft.Build().ShouldBeSameAs(item);
    });

    [Fact]
    public void Draft_GetOnlyMember_ReadsOriginal() => WithProbe(probe =>
    {
        var draft = probe.CreateDraft(probe.Make([1, "abc", false, 0, null]));

        probe.Length(draft).ShouldBe(3);
        probe.SetTitle(draft, "abcdef");
        probe.Length(draft).ShouldBe(3);
    });

    [Fact]
    public void Draft_AfterProduce_Revoked() => WithProbe(probe =>
    {
        var kept = probe.Keep(probe.Make([1, "a", false, 0, null]));

        ShouldBeRevoked(() => probe.Title(kept));
        ShouldBeRevoked(() => probe.Length(kept));
        ShouldBeRevoked(() => probe.SetTitle(kept, "b"));
        ShouldBeRevoked(() => probe.Build(kept));
        ShouldBeRevoked(() => ((IDraft<object>)kept).Build());
    });

    [Fact]
    public void Draft_Revoke_ThrowsOnNextAccess() => WithProbe(probe =>
    {
        var draft = probe.CreateDraft(probe.Make([1, "a", false, 0, null]));

        probe.Title(draft).ShouldBe("a");
        ((IRevocable)draft).Revoke();
        ShouldBeRevoked(() => probe.Title(draft));
    });

    [Fact]
    public void Draft_ImplicitConversion_NullStaysNull() => WithProbe(probe =>
    {
        var item = probe.Make([1, "a", false, 0, null]);

        probe.Convert(null).ShouldBeNull();
        ((IDraft<object>)probe.Convert(item)!).Build().ShouldBeSameAs(item);
    });

    private static void ShouldBeRevoked(Action action) =>
        Should.Throw<ObjectDisposedException>(action).ShouldSatisfyAllConditions(
            e => e.ObjectName.ShouldBe("Draft"),
            e => e.Message.ShouldStartWith("draft revoked: Produce returned, or an assignment or structural operation replaced it"));

    private sealed class Probe(Assembly assembly)
    {
        private readonly Type _probe = assembly.GetType("App.Probe")!;

        public object Make(object?[] values) => Method<Func<object?[], object>>("Make")(values);

        public object?[] Read(object item) => Method<Func<object, object?[]>>("Read")(item);

        public object Apply(object item, (int Member, object? Value)[] edits) =>
            Method<Func<object, (int, object?)[], object>>("Apply")(item, edits);

        public object CreateDraft(object item) => Method<Func<object, object>>("CreateDraft")(item);

        public object Keep(object item) => Method<Func<object, object>>("Keep")(item);

        public int Length(object draft) => Method<Func<object, int>>("Length")(draft);

        public string Title(object draft) => Method<Func<object, string>>("Title")(draft);

        public void SetTitle(object draft, string title) => Method<Action<object, string>>("SetTitle")(draft, title);

        public object Build(object draft) => Method<Func<object, object>>("Build")(draft);

        public object? Convert(object? item) => Method<Func<object?, object?>>("Convert")(item);

        private T Method<T>(string name)
            where T : Delegate => _probe.GetMethod(name)!.CreateDelegate<T>();
    }
}
