using Microsoft.CodeAnalysis;
using Verifier = Ducky.Draft.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Draft.Generators.DraftEscapeAnalyzer>;

namespace Ducky.Draft.Generators.Tests.Analyzers;

// DUCKY106 verifier tests (SPEC §16.2): a Produce recipe that stores, in a field, a property outside the draft or a
// captured variable, a draft it reached from its parameter (the draft, a nested, collection or element draft, or a local
// copied from one) or a lambda or local function that reads it. The draft is revoked when Produce returns, so a later use
// throws. The verifier runs the Draft generator first, as in a consumer build. `{|DUCKY106:...|}` marks each expected location.
public sealed class DraftEscapeAnalyzerTests
{
    private const string Prologue = """
        #pragma warning disable CS1591, CS0169, CS0414, CS0649 // justification: undocumented test sources; each row uses a few fields
        using System;
        using System.Collections.Immutable;
        using System.Linq;
        namespace App;
        [Ducky.Draft.Draftable] public sealed partial record Child(int Value);
        [Ducky.Draft.Draftable] public sealed partial record Todo(string Title, Child Child, ImmutableList<Child> Items);
        public sealed class Holder { public Todo.Draft? Draft { get; set; } public Todo.Draft? Kept; }
        public sealed class Outer { public Holder Inner { get; } = new(); }
        public sealed class Box { public Box? Saved; public void Produce(Action<Box> recipe) => recipe(this); }
        public sealed class Pair { public void Produce(Action<Todo.Draft, int> recipe) => recipe(new Todo("", new Child(0), []).CreateDraft(), 0); }
        public static class Other { public static void Apply(this Todo t, Action<Todo.Draft> recipe) => recipe(t.CreateDraft()); }
        public sealed class Recipes
        {
            private Todo.Draft? _saved;
            private Child.Draft? _child;
            private object? _any;
            private string? _title;
            private Box? _box;
            private Func<Todo.Draft>? _later;
            private Action? _act;
            private Ducky.Draft.ListDraft<Child, Child.Draft>? _items;
            private readonly Todo.Draft?[] _all = new Todo.Draft?[1];
        private readonly Holder[] _holders = [new()];
        private readonly System.Collections.Generic.Dictionary<int, Holder> _byValue = [];
            public static Todo.Draft? Last { get; set; }

        """;

    public static TheoryData<string, string> Escapes => new()
    {
        { "field", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_saved = d|});" },
        { "static property", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:Last = d|});" },
        { "property of another object", "public Todo M(Todo t, Holder holder) => t.Produce(d => {|DUCKY106:holder.Draft = d|});" },
        { "property of an array element indexed by the draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_holders[d.Child.Value].Draft = d|});" },
        { "property of a dictionary value keyed by the draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_byValue[d.Child.Value].Draft = d|});" },
        { "property of an object found through the draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_holders.First(h => h.Draft == d).Draft = d|});" },
        { "captured local", "public Todo M(Todo t) { Todo.Draft? saved = null; var r = t.Produce(d => { {|DUCKY106:saved = d|}; }); return saved is null ? r : t; }" },
        { "captured parameter", "public Todo M(Todo t, Todo.Draft? outer) => t.Produce(d => {|DUCKY106:outer = d|});" },
        { "nested draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_child = d.Child|});" },
        { "element draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_child = d.Items[0]|});" },
        { "collection draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_items = d.Items|});" },
        { "converted to object", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_any = d|});" },
        { "coalescing assignment", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_saved ??= d|});" },
        { "a lambda returning the draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_later = () => d|});" },
        { "a lambda editing the draft", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_act = () => d.Title = \"x\"|});" },
        { "a lambda editing a nested draft, compound", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_act += () => d.Child.Value++|});" },
        { "a local function reading the draft", "public Todo M(Todo t) => t.Produce(d => { void Edit() => d.Title = \"x\"; {|DUCKY106:_act = Edit|}; });" },
        { "a draft copied into a local", "public Todo M(Todo t) => t.Produce(d => { var c = d.Child; {|DUCKY106:_child = c|}; });" },
        { "a draft assigned to a local", "public Todo M(Todo t) => t.Produce(d => { Child.Draft c; c = d.Child; {|DUCKY106:_child = c|}; });" },
        { "a foreach variable", "public Todo M(Todo t) => t.Produce(d => { foreach (var c in d.Items) { {|DUCKY106:_child = c|}; } });" },
        { "a lambda over a draft local", "public Todo M(Todo t) => t.Produce(d => { var c = d.Child; {|DUCKY106:_act = () => c.Value = 1|}; });" },
        { "a draft returned by a local function", "public Todo M(Todo t) => t.Produce(d => { void Noop() { } Child.Draft Find(int i) => d.Items[i]; Noop(); {|DUCKY106:_child = Find(0)|}; });" },
        { "a lambda calling a local function", "public Todo M(Todo t) => t.Produce(d => { void Edit() => d.Title = \"x\"; {|DUCKY106:_act = () => Edit()|}; });" },
        { "array element", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_all[0] = d|});" },
        { "element of a recipe local aliasing an array field", "public Todo M(Todo t) => t.Produce(d => { var a = _all; {|DUCKY106:a[0] = d|}; });" },
        { "property of a recipe local aliasing another object", "public Todo M(Todo t, Holder holder) => t.Produce(d => { var h = new Holder(); h = holder; {|DUCKY106:h.Draft = d|}; });" },
        { "property of each object of a field", "public Todo M(Todo t) => t.Produce(d => { foreach (var h in _holders) { {|DUCKY106:h.Draft = d|}; } });" },
        { "property of a captured local built outside", "public Todo M(Todo t) { var h = new Holder(); return t.Produce(d => {|DUCKY106:h.Draft = d|}); }" },
        { "field of another object", "public Todo M(Todo t, Holder holder) => t.Produce(d => {|DUCKY106:holder.Kept = d|});" },
        { "a dynamic member", "public Todo M(Todo t, dynamic bag) => t.Produce(d => {|DUCKY106:bag.Draft = d|});" },
        { "a nested member initializer, filling an object found elsewhere", "public Todo M(Todo t) => t.Produce(d => { var o = new Outer { Inner = { {|DUCKY106:Draft = d|} } }; });" },
        { "a lambda reading the draft as a key", "public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_act = () => Console.WriteLine(_holders[d.Child.Value])|});" },
        { "a local function reading the draft as a key", "public Todo M(Todo t) => t.Produce(d => { void Log() => Console.WriteLine(_holders[d.Child.Value]); {|DUCKY106:_act = Log|}; });" },
        { "initializer of an object stored in a field", "public Todo M(Todo t) => t.Produce(d => _any = new Holder { {|DUCKY106:Draft = d|} });" },
        { "initializer of an anonymous object stored in a field", "public Todo M(Todo t) => t.Produce(d => _any = new { {|DUCKY106:D = d|} });" },
        { "a recipe with a second, plain parameter", "public void M(Pair pair) => pair.Produce((d, n) => {|DUCKY106:_saved = d|});" },
        { "a holder looked up with out var", "public Todo M(Todo t) => t.Produce(d => { if (_byValue.TryGetValue(1, out var h)) { {|DUCKY106:h.Draft = d|}; } });" },
        { "a built local overwritten by an out argument", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); if (_byValue.TryGetValue(1, out h)) { {|DUCKY106:h.Draft = d|}; } });" },
        { "a built local overwritten through a ref argument", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); Swap(ref h); {|DUCKY106:h.Draft = d|}; }); private void Swap(ref Holder h) => h = _holders[0];" },
        { "a pattern variable", "public Todo M(Todo t) => t.Produce(d => { if (_any is Holder h) { {|DUCKY106:h.Draft = d|}; } });" },
        { "a deconstruction variable", "public Todo M(Todo t) => t.Produce(d => { var (h, _) = (_holders[0], 1); {|DUCKY106:h.Draft = d|}; });" },
        { "a built local deconstructed into", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); (h, _) = (_holders[0], 1); {|DUCKY106:h.Draft = d|}; });" },
        { "a built local overwritten through a ref local", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); ref var r = ref h; r = _holders[0]; {|DUCKY106:h.Draft = d|}; });" },
        { "a built local overwritten through a ref reassignment", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); var g = new Holder(); ref var r = ref g; r = ref h; r = _holders[0]; {|DUCKY106:h.Draft = d|}; });" },
        { "a built local overwritten through a ref conditional", "public Todo M(Todo t, bool b) => t.Produce(d => { var h = new Holder(); var g = new Holder(); ref var r = ref b ? ref h : ref g; r = _holders[0]; {|DUCKY106:h.Draft = d|}; });" },
        { "each object of a fresh array of objects found elsewhere", "public Todo M(Todo t) => t.Produce(d => { foreach (var h in new[] { _holders[0] }) { {|DUCKY106:h.Draft = d|}; } });" },
        { "two escapes", "public Todo M(Todo t) => t.Produce(d => { {|DUCKY106:_saved = d|}; {|DUCKY106:_child = d.Child|}; });" },
    };

    public static TheoryData<string, string> Contained => new()
    {
        { "edits through the draft", "public Todo M(Todo t) => t.Produce(d => { d.Title = \"x\"; d.Child.Value = 2; d.Items[0].Value = 3; });" },
        { "a draft member set from the draft", "public Todo M(Todo t) => t.Produce(d => d.Child = d.Child);" },
        { "a local declared in the recipe", "public Todo M(Todo t) => t.Produce(d => { Todo.Draft alias; alias = d; alias.Title = \"x\"; });" },
        { "a deconstruction into recipe locals", "public Todo M(Todo t) => t.Produce(d => { var (alias, title) = (d, \"x\"); alias.Title = title; });" },
        { "a draft returned from a local function called inline", "public Todo M(Todo t) => t.Produce(d => { Todo.Draft Self() { return d; } Self().Title = \"x\"; });" },
        { "a Find helper called inline", "public Todo M(Todo t) => t.Produce(d => { Child.Draft Find(int v) => d.Items.First(x => x.Value == v); Find(3).Value = 1; });" },
        { "a LINQ query over element drafts", "public Todo M(Todo t) => t.Produce(d => Enumerable.Range(0, 1).Select(i => d.Items[i]).First().Value = 1);" },
        { "an array local declared in the recipe", "public Todo M(Todo t) => t.Produce(d => { var a = new Todo.Draft[1]; a[0] = d; a[0].Title = \"x\"; });" },
        { "a property of an object built in the recipe", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); h.Draft = d; h.Draft.Title = \"x\"; });" },
        { "a cast of an object built in the recipe", "public Todo M(Todo t) => t.Produce(d => { object o = new Holder(); ((Holder)o).Draft = d; });" },
        { "a field of an object built in the recipe", "public Todo M(Todo t) => t.Produce(d => { Holder h; h = new Holder(); h.Kept = d; });" },
        { "an object built in the recipe, copied to another local", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); var g = h; h.Draft = d; });" },
        { "an object built in the recipe, assigned to another local", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); object o; o = h; h.Draft = d; });" },
        { "an object built in the recipe, passed by value", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); _ = _byValue.ContainsValue(h); h.Draft = d; });" },
        { "an object built in the recipe, read in a deconstruction", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); var (g, n) = (h, 1); h.Draft = d; });" },
        { "an object built in the recipe, passed in __arglist", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); Log(__arglist(h)); h.Draft = d; }); private static void Log(__arglist) { }" },
        { "an object built in the recipe, filled in a loop", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder(); foreach (var i in new[] { 1 }) { h.Draft = d; } });" },
        { "an element of a collection expression local", "public Todo M(Todo t) => t.Produce(d => { Todo.Draft?[] a = [null]; a[0] = d; });" },
        { "an object initializer in a recipe local", "public Todo M(Todo t) => t.Produce(d => { var h = new Holder { Draft = d }; h.Draft.Title = \"x\"; });" },
        { "an object initializer assigned to a recipe local", "public Todo M(Todo t) => t.Produce(d => { object h; h = new Holder { Draft = d }; });" },
        { "an anonymous object in a recipe local", "public Todo M(Todo t) => t.Produce(d => { var x = new { D = d }; x.D.Title = \"y\"; });" },
        { "a draft found by a key read from the draft", "public Todo M(Todo t) => t.Produce(d => { var m = new System.Collections.Generic.Dictionary<string, Todo.Draft> { [\"\"] = t.CreateDraft() }; _saved = m[d.Title]; });" },
        { "an array element indexed by the draft", "public Todo M(Todo t) => t.Produce(d => { var a = new[] { t.CreateDraft() }; _saved = a[d.Child.Value]; });" },
        { "a lambda kept in a recipe local", "public Todo M(Todo t) => t.Produce(d => { Action edit; edit = () => d.Title = \"x\"; edit(); });" },
        { "a recursive local function", "public Todo M(Todo t) => t.Produce(d => { Child.Draft At(int i) => i > 0 ? At(i - 1) : d.Child; At(1).Value = 1; });" },
        { "a method group not reading the draft", "public Todo M(Todo t) => t.Produce(d => { _act = Console.WriteLine; d.Title = \"x\"; });" },
        { "a draft built in the recipe, aliased twice", "public Todo M(Todo t) => t.Produce(d => { var a = new Child.Draft(new Child(0)); var b = a; b = a; _child = b; });" },
        { "a fresh draft from a local function", "public Todo M(Todo t) => t.Produce(d => { void Edit() => d.Title = \"x\"; Child.Draft Make() => new Child.Draft(new Child(0)); Edit(); _child = Make(); });" },
        { "a lambda not reading the draft", "public Todo M(Todo t) => t.Produce(d => { _act = () => _title = \"x\"; d.Title = \"y\"; });" },
        { "a nested lambda's own parameter", "public Todo M(Todo t) => t.Produce(d => { Action<Todo.Draft> f = x => { x = d; x.Title = \"y\"; }; f(d); });" },
        { "a draft not reached from the recipe parameter", "public Todo M(Todo t) => t.Produce(d => _saved = new Todo.Draft(t));" },
        { "a discarded read", "public Todo M(Todo t) => t.Produce(d => { _ = d.Child; _ = d; d.Title = \"x\"; });" },
        { "a draft from outside the recipe", "public Todo M(Todo t) => t.Produce(d => _saved = Last);" },
        { "null stored in a field", "public Todo M(Todo t) => t.Produce(d => _saved = null);" },
        { "a plain value read from the draft", "public Todo M(Todo t) => t.Produce(d => _title = d.Title);" },
        { "a plain value returned from a nested lambda", "public Todo M(Todo t) => t.Produce(d => { Func<string> f = () => d.Title; _title = f(); });" },
        { "a Produce whose parameter is no draft", "public void M(Box box) => box.Produce(b => _box = b);" },
        { "a recipe passed to another method", "public void M(Todo t) => t.Apply(d => _saved = d);" },
        { "a method group recipe", "public Todo M(Todo t) => t.Produce(Edit); private static void Edit(Todo.Draft d) => d.Title = \"x\";" },
        { "a multi-step draft", "public Todo M(Todo t) { _saved = t.CreateDraft(); return _saved.Build(); }" },
    };

    [Theory]
    [MemberData(nameof(Escapes))]
    public Task Ducky106_DraftEscapesRecipe_Reported(string shape, string member) => Verifier.VerifyAsync(Source(member + " // " + shape));

    [Theory]
    [MemberData(nameof(Contained))]
    public Task Ducky106_DraftStaysInRecipe_NotReported(string shape, string member) => Verifier.VerifyAsync(Source(member + " // " + shape));

    [Fact]
    public Task Ducky106_Message_NamesTheEscapingExpression() =>
        Verifier.VerifyAsync(
            Source("public Todo M(Todo t) => t.Produce(d => {|#0:_child = d.Child|});"),
            Verifier.Diagnostic(DraftDiagnostics.Escape).WithLocation(0).WithArguments("d.Child").WithMessage(
                "'d.Child' escapes its Produce recipe: the draft is revoked when Produce returns, so any later use throws ObjectDisposedException"));

    // A component's @code block compiles to generated C# (*.razor.g.cs), where most Blazor recipes live.
    [Fact]
    public Task Ducky106_GeneratedCode_Reported() =>
        Verifier.VerifyAsync("// <auto-generated/>\n#nullable enable\n" + Source("public Todo M(Todo t) => t.Produce(d => {|DUCKY106:_saved = d|});"));

    // A top-level statement recipe assigns `args`, a synthesized parameter declared nowhere in source.
    [Fact]
    public Task Ducky106_TopLevelArgsAssigned_NotReported()
    {
        var test = Verifier.Create("""
            using System;
            using App;
            var r = new Todo("", new Child(0), []).Produce(d => { args = new string[0]; d.Title = "x"; });
            Console.WriteLine(r);
            """);
        test.TestState.Sources.Add(Source(string.Empty));
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        return Verifier.VerifyAsync(test);
    }

    private static string Source(string member) => Prologue + "    " + member + "\n}\n";
}
