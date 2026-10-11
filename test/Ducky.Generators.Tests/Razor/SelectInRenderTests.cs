using System.Text.RegularExpressions;
using Ducky.Generators.Analyzers.Razor;
using Microsoft.CodeAnalysis.Testing;
using Verify = Ducky.Generators.Tests.Infrastructure.RazorVerifier<Ducky.Generators.SelectInRenderAnalyzer>;

namespace Ducky.Generators.Tests.Razor;

// DUCKY009 (SPEC §11.2, §16.2, INV-20): Select after the first render, as Analyzer.Testing verifier tests with the real
// Razor generator in the driver, reported at the #line-mapped .razor location. Reasoned deviation: the verifier compares
// unmapped spans (the generated .g.cs for markup), so RazorVerifier re-creates each mapped diagnostic at its .razor
// location before the comparison; the expected location is the start of `Select`, its length ignored.
public sealed class SelectInRenderTests
{
    private const string Page = "/Pages/Counter.razor";
    private const string Component = "@inherits Ducky.Blazor.DuckyComponent\n";

    // A slice state type for the typed overload and DuckyComponent<TState>, and child components with EventCallback
    // parameters: Razor binds a plain one through EventCallback.Factory, and `@bind-Value` on a generic one whose
    // type is inferred through RuntimeHelpers.CreateInferredEventCallback.
    private const string AppTypes = """
        #pragma warning disable CS1591 // justification: undocumented test sources
        using Microsoft.AspNetCore.Components;
        namespace App;
        public sealed record Cart(int Count);
        public sealed class Done : ComponentBase { [Parameter] public EventCallback OnDone { get; set; } }
        public sealed class Stepper : ComponentBase { [Parameter] public EventCallback<int> OnStep { get; set; } }
        public sealed class Picker<TItem> : ComponentBase
        {
            [Parameter] public TItem Value { get; set; } = default!;
            [Parameter] public EventCallback<TItem> ValueChanged { get; set; }
        }
        """;

    private static Task VerifyAsync(string razor, params DiagnosticResult[] expected) =>
        Verify.VerifyAsync([(Page, razor)], [AppTypes], expected);

    // DUCKY009 at the first `Select` call in `text` (its receiver included, as in `new Counter().Select`), naming `member`.
    private static DiagnosticResult Expected(string path, string text, string member)
    {
        var index = Regex.Match(text, @"(new \w+\(\)\.)?Select").Index;
        index.ShouldBeGreaterThan(0);
        var line = text.AsSpan(0, index).Count('\n');
        var column = index - (text.LastIndexOf('\n', Math.Max(index - 1, 0)) + 1);
        return Verify.Diagnostic(SelectDiagnostics.SelectAfterFirstRender).WithLocation(path, line + 1, column + 1).WithArguments(member);
    }

    public static TheoryData<string, string, string> Reported => new()
    {
        // Markup: an explicit expression, a code block, an inline handler lambda and a child-content render fragment all
        // compile into BuildRenderTree.
        { "markup expression", Component + "<p>@(Select(s => 1).Value)</p>\n", "BuildRenderTree" },
        { "code block", Component + "@{ var n = Select(s => 1).Value; }\n<p>@n</p>\n", "BuildRenderTree" },
        { "inline handler", Component + "<button @onclick=\"() => Select(s => 1)\">+</button>\n", "BuildRenderTree" },
        { "render fragment", Component + "<CascadingValue Value=\"1\"><p>@(Select(s => 1).Value)</p></CascadingValue>\n", "BuildRenderTree" },
        { "typed overload", Component + "<p>@(Select<App.Cart, int>(c => c.Count).Value)</p>\n", "BuildRenderTree" },
        { "layout", "@inherits Ducky.Blazor.DuckyLayout\n<p>@(Select(s => 1).Value)</p>\n@Body\n", "BuildRenderTree" },
        { "component of state", "@inherits Ducky.Blazor.DuckyComponent<App.Cart>\n<p>@(Select(s => 1).Value)</p>\n", "BuildRenderTree" },

        // Lifecycle overrides that run after the first render.
        { "OnParametersSet", Component + "@code {\n    protected override void OnParametersSet() => Select(s => 1);\n}\n", "OnParametersSet" },
        {
            "OnParametersSetAsync",
            Component + "@code {\n    protected override Task OnParametersSetAsync()\n    {\n        Select(s => 1);\n        return Task.CompletedTask;\n    }\n}\n",
            "OnParametersSetAsync"
        },
        { "OnAfterRender", Component + "@code {\n    protected override void OnAfterRender(bool firstRender) => Select(s => 1);\n}\n", "OnAfterRender" },
        {
            "OnAfterRenderAsync",
            Component + "@code {\n    protected override Task OnAfterRenderAsync(bool firstRender)\n    {\n        Select(s => 1);\n        return Task.CompletedTask;\n    }\n}\n",
            "OnAfterRenderAsync"
        },

        // Event handlers: a method group, a method a handler lambda calls, bind:after and bind:set callbacks.
        { "handler method group", Component + "<button @onclick=\"Pick\">+</button>\n@code {\n    void Pick() => Select(s => 1);\n}\n", "Pick" },
        { "handler lambda call", Component + "<button @onclick=\"() => Pick(2)\">+</button>\n@code {\n    void Pick(int n) => Select(s => n);\n}\n", "Pick" },
        {
            "bind after",
            Component + "<input @bind=\"text\" @bind:after=\"Changed\" />\n@code {\n    string text = \"\";\n    void Changed() => Select(s => 1);\n}\n",
            "Changed"
        },
        {
            "bind set",
            Component + "<input @bind:get=\"text\" @bind:set=\"Set\" />\n@code {\n    string text = \"\";\n    void Set(string? value) => Select(s => value);\n}\n",
            "Set"
        },
        { "generic handler", Component + "<button @onclick=\"Pick<int>\">+</button>\n@code {\n    void Pick<T>() => Select(s => 1);\n}\n", "Pick" },

        // EventCallback parameters of child components: plain, typed, and on a generic component whose type is inferred.
        { "child EventCallback", Component + "<App.Done OnDone=\"Finish\" />\n@code {\n    void Finish() => Select(s => 1);\n}\n", "Finish" },
        { "child EventCallback<T>", Component + "<App.Stepper OnStep=\"Step\" />\n@code {\n    void Step(int n) => Select(s => n);\n}\n", "Step" },
        {
            "inferred generic child bind after",
            Component + "<App.Picker @bind-Value=\"picked\" @bind-Value:after=\"Changed\" />\n@code {\n    int picked = 1;\n    void Changed() => Select(s => 1);\n}\n",
            "Changed"
        },
        {
            "inferred generic child bind set",
            Component + "<App.Picker @bind-Value:get=\"picked\" @bind-Value:set=\"Set\" />\n@code {\n    int picked = 1;\n    void Set(int value) => Select(s => value);\n}\n",
            "Set"
        },

        // Templated Razor delegates in @code render with the component, wherever they are declared.
        { "templated delegate property", Component + "@code {\n    RenderFragment Frag => @<p>@(Select(s => 1).Value)</p>;\n}\n", "Frag" },
        { "templated delegate method", Component + "@code {\n    RenderFragment Row(int i) => @<p>@(Select(s => i).Value)</p>;\n}\n", "Row" },
        {
            "templated delegate in a static field",
            Component + "@code {\n    static RenderFragment Shared = @<p>@(new Counter().Select(s => 1).Value)</p>;\n}\n",
            "Shared"
        },
        {
            "templated delegate of T",
            Component + "@code {\n    RenderFragment<int> Row => i =>\n    {\n        var n = Select(s => i);\n        return @<p>@n.Value</p>;\n    };\n}\n",
            "Row"
        },

        // The inferred factory itself, as another Razor emitter may call it outside EventCallback.Factory.
        {
            "inferred callback",
            Component + "@code {\n    public EventCallback<int> Callback { get; private set; }\n    protected override void OnInitialized() =>\n"
                + "        Callback = Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.CreateInferredEventCallback(this, Pick, 1);\n"
                + "    void Pick(int n) => Select(s => n);\n}\n",
            "Pick"
        },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky009_SelectAfterFirstRender_ReportedAtRazorLocation(string name, string razor, string member)
    {
        _ = name;
        return VerifyAsync(razor, Expected(Page, razor, member));
    }

    public static TheoryData<string, string> NotReported => new()
    {
        // Registration before the first render.
        { "OnInitialized", Component + "<p>@n.Value</p>\n@code {\n    Ducky.Selection<int> n = null!;\n    protected override void OnInitialized() => n = Select(s => 1);\n}\n" },
        {
            "OnInitializedAsync",
            Component + "<p>@n.Value</p>\n@code {\n    Ducky.Selection<int> n = null!;\n    protected override Task OnInitializedAsync()\n    {\n        n = Select(s => 1);\n        return Task.CompletedTask;\n    }\n}\n"
        },

        // A helper no handler references, next to a handler that doesn't select.
        { "helper", Component + "<button @onclick=\"Other\">+</button>\n@code {\n    protected override void OnInitialized() => Helper();\n    void Helper() => Select(s => 1);\n    void Other() { }\n}\n" },

        // A method group handed to something other than an event callback.
        { "non-callback delegate", Component + "@code {\n    protected override void OnInitialized() => Run(Pick);\n    static void Run(Action a) { }\n    void Pick() => Select(s => 1);\n}\n" },

        // A lambda that isn't a render fragment, and a RuntimeHelpers method that isn't a callback factory.
        { "non-fragment lambda", Component + "@code {\n    protected override void OnInitialized()\n    {\n        System.Func<int> f = () => Select(s => 1).Value;\n        _ = f;\n    }\n}\n" },
        {
            "type check",
            Component + "@code {\n    protected override void OnInitialized() =>\n"
                + "        _ = Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<System.Action>(Pick);\n"
                + "    void Pick() => Select(s => 1);\n}\n"
        },

        // An overload of a lifecycle name the renderer never calls.
        { "lifecycle overload", Component + "@code {\n    protected override void OnInitialized() => OnAfterRender(\"init\");\n    void OnAfterRender(string why) => Select(s => 1);\n}\n" },

        // Other DuckyComponent members, and a Select that isn't Ducky's.
        { "Dispatch in markup", Component + "@{ Dispatch(new App.Cart(1)); }\n" },
        { "foreign Select", "@inherits ComponentBase\n<p>@Select(1)</p>\n@code {\n    int Select(int x) => x;\n}\n" },
    };

    [Theory]
    [MemberData(nameof(NotReported))]
    public Task Ducky009_SelectBeforeFirstRenderOrNotDucky_NotReported(string name, string razor)
    {
        _ = name;
        return VerifyAsync(razor);
    }

    // A field initializer can't reach Select (CS0236), but the analyzer meets one while the user types: the call arrives
    // as an invalid operation, so only the compiler error is reported and the analyzer doesn't fail on a non-method owner.
    [Fact]
    public Task Ducky009_SelectInFieldInitializer_OnlyCompilerError()
    {
        const string razor = Component + "@code {\n    RenderFragment frag = @<p>@(Select(s => 1).Value)</p>;\n}\n";
        var start = Expected(Page, razor, "frag").Spans[0].Span.StartLinePosition;

        return VerifyAsync(razor, DiagnosticResult.CompilerError("CS0236").WithLocation(Page, start.Line + 1, start.Character + 1));
    }

    // A code-behind partial class is plain source: its handler is found from the markup's callback all the same.
    [Fact]
    public Task Ducky009_HandlerInCodeBehind_ReportedInCodeBehind()
    {
        const string codeBehind = """
            #pragma warning disable CS1591 // justification: undocumented test sources
            namespace App.Pages;
            public partial class Counter
            {
                private void Pick() => Select(s => 1);
            }
            """;

        return Verify.VerifyAsync(
            [(Page, Component + "<button @onclick=\"Pick\">+</button>\n")], [AppTypes, codeBehind], Expected("/0/Test1.cs", codeBehind, "Pick"));
    }
}
