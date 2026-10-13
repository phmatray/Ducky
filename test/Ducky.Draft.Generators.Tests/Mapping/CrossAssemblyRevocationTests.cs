using Ducky.Draft.Generators.Tests.Infrastructure;

namespace Ducky.Draft.Generators.Tests.Mapping;

// INV-26 across assemblies (SPEC §13.3): revocation goes through the public IRevocable contract, so after Produce returns,
// App's draft has revoked the drafts of Lib records it handed out: a nested member, a Lib draft nested in it, a nullable
// member, and list, array and dictionary element drafts. The Ducky.Draft.Tests property of the same name covers the
// same-assembly members; Lib and App here are two compilations, each run through this generator and loaded together.
public sealed class CrossAssemblyRevocationTests
{
    private const string Lib = """
        #pragma warning disable CS1591 // justification: undocumented probe source; only generated code is checked
        namespace Lib;
        [Ducky.Draft.Draftable] public sealed partial record Tag(string Name);
        [Ducky.Draft.Draftable] public sealed partial record Item(int Id, string Title, Tag Label);
        """;

    private const string App = """
        #pragma warning disable CS1591 // justification: undocumented probe source; only generated code is checked
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        namespace App;
        [Ducky.Draft.Draftable]
        public sealed partial record Holder(Lib.Item Item, Lib.Item? Maybe, ImmutableList<Lib.Item> Items, ImmutableArray<Lib.Item> Cards, ImmutableDictionary<string, Lib.Item> ById);
        public static class Probe
        {
            private static string Throws(System.Action action)
            {
                try { action(); return "none"; }
                catch (System.Exception e) { return e.GetType().Name; }
            }

            public static string[] Revoked()
            {
                var item = new Lib.Item(1, "a", new Lib.Tag("t"));
                var holder = new Holder(item, item, [item], [item], ImmutableDictionary<string, Lib.Item>.Empty.Add("a", item));
                var accesses = new List<(string, System.Action)>();
                var result = holder.Produce(d =>
                {
                    var main = d.Item;
                    var label = main.Label;
                    var maybe = d.Maybe!;
                    var element = d.Items[0];
                    var card = d.Cards[0];
                    var value = d.ById["a"];
                    main.Title = "edited";
                    accesses.Add(("Item read", () => _ = main.Title));
                    accesses.Add(("Item write", () => main.Title = "x"));
                    accesses.Add(("Item.Label write", () => label.Name = "x"));
                    accesses.Add(("Maybe write", () => maybe.Title = "x"));
                    accesses.Add(("Items[0] write", () => element.Title = "x"));
                    accesses.Add(("Cards[0] write", () => card.Title = "x"));
                    accesses.Add(("ById[a] write", () => value.Title = "x"));
                });
                return [result.Item.Title, .. accesses.Select(a => $"{a.Item1}: {Throws(a.Item2)}")];
            }
        }
        """;

    [Fact]
    public void Produce_RevokedDraftAccess_Throws()
    {
        GeneratorHarness.Run(new DraftGenerator(), GeneratorHarness.Compile(Lib).WithAssemblyName("Lib"), out var lib);
        GeneratorHarness.Run(new DraftGenerator(), GeneratorHarness.Compile(App).AddReferences(lib.ToMetadataReference()), out var app);
        string[] result = [];

        GeneratorHarness.Execute(app, assembly => result = (string[])assembly.GetType("App.Probe")!.GetMethod("Revoked")!.Invoke(null, null)!, lib);

        result.ShouldBe([
            "edited",
            "Item read: ObjectDisposedException",
            "Item write: ObjectDisposedException",
            "Item.Label write: ObjectDisposedException",
            "Maybe write: ObjectDisposedException",
            "Items[0] write: ObjectDisposedException",
            "Cards[0] write: ObjectDisposedException",
            "ById[a] write: ObjectDisposedException",
        ]);
    }
}
