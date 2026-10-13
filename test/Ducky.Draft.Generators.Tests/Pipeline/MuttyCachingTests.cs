using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// Mutty's caching suite (IncrementalGeneratorCachingTests, ported per SPEC §13.1 and §17.7; Mutty is Apache-2.0, see
// NOTICE): adding an unrelated syntax tree must not run the record-model transform again. Mutty's one case checked its
// RecordModels step; here every Draft step and every source output must stay cached, over Mutty's own Person record
// (migrated per §13.4) and over a member whose type comes from another assembly, whose mapping reads metadata.
public sealed class MuttyCachingTests
{
    private static readonly string[] _steps = ["DraftAnalyses", "DraftRecords", "DraftReports", "DraftOutput"];

    // Mutty's fixture, with [MutableGeneration] migrated to [Draftable] on a partial record.
    private const string Person = """
        #pragma warning disable CS1591 // justification: undocumented test source
        namespace Demo;

        [Ducky.Draft.Draftable]
        public partial record Person(string Name, int Age);
        """;

    // The literal port, over Mutty's fixture; the harness's default edit is Mutty's (an unrelated tree added).
    [Fact]
    public void Caching_DraftRecords_UnrelatedTreeAdded_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), GeneratorHarness.Compile(Person), _steps);

    // A Lib draftable as a member and as a list element: the cross-assembly mapping (a nested public Draft implementing
    // IDraft<C> with a Draft(C) constructor) is a boolean in the model, not a symbol, so it caches too.
    [Fact]
    public void Caching_DraftAnalyses_CrossAssemblyMember_Cached()
    {
        GeneratorHarness.Run(new DraftGenerator(), GeneratorHarness.Compile("""
            namespace Lib;
            /// <summary>A draftable Lib record.</summary>
            [Ducky.Draft.Draftable] public sealed partial record Item(int Id);
            """).WithAssemblyName("Lib"), out var lib);
        using var image = new MemoryStream();
        lib.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();

        var app = GeneratorHarness.Compile(Person + """

            [Ducky.Draft.Draftable]
            public partial record Team(Lib.Item Lead, System.Collections.Immutable.ImmutableList<Lib.Item> Members);
            """).AddReferences(MetadataReference.CreateFromImage(image.ToArray()));

        // The mapping must hold, or both members fall back to plain DUCKY107 members that cache just as well (the
        // three-argument Run fails on any reported diagnostic).
        GeneratorHarness.Run(new DraftGenerator(), app, out var output);
        var draft = output.GetTypeByMetadataName("Demo.Team+Draft").ShouldNotBeNull();
        draft.GetMembers("Lead").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("Lib.Item.Draft");
        draft.GetMembers("Members").OfType<IPropertySymbol>().Single().Type.ToDisplayString()
            .ShouldBe("Ducky.Draft.ListDraft<Lib.Item, Lib.Item.Draft>");

        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), app, _steps);
    }
}
