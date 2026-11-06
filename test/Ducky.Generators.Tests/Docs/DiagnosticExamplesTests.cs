using System.Reflection;
using System.Text.Json.SourceGeneration;
using Ducky.Generators.Dispatch;
using Ducky.Generators.Registration;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ducky.Generators.Tests.Docs;

// SPEC §16.2: every Ducky.Generators descriptor has docs/diagnostics/{ID}.md, its HelpLinkUri points there, and the page's
// bad example is a #region of this file, a raw string that every generator and analyzer of the assembly (with the
// System.Text.Json generator, as in a consumer build, in a web project) reports with exactly that one Ducky ID.
public sealed class DiagnosticExamplesTests
{
    private const string HelpRoot = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/";

    #region DUCKY001
    private const string Ducky001 = """
        using System;
        using System.Collections.Immutable;
        using Ducky;

        public sealed record Todo(string Title, DateTime CreatedAt);
        public sealed record Todos(ImmutableList<Todo> Items);
        public sealed record AddTodo(string Title);

        public sealed class TodosSlice : Slice<Todos>
        {
            protected override Todos Initial => new([]);

            public TodosSlice() =>
                On<AddTodo>((s, a) => s with { Items = s.Items.Add(new Todo(a.Title, DateTime.Now)) });
        }
        """;
    #endregion

    #region DUCKY002
    private const string Ducky002 = """
        using System.IO;
        using Ducky;

        public sealed record Settings(string Text);
        public sealed record LoadSettings(string Path);

        public sealed class SettingsSlice : Slice<Settings>
        {
            protected override Settings Initial => new("");

            public SettingsSlice() =>
                On<LoadSettings>((s, a) => s with { Text = File.ReadAllText(a.Path) });
        }
        """;
    #endregion

    #region DUCKY003
    private const string Ducky003 = """
        using System.Collections.Generic;
        using Ducky;

        public sealed record Cart(List<string> Items);
        public sealed record AddItem(string Item);

        public sealed class CartSlice : Slice<Cart>
        {
            protected override Cart Initial => new([]);

            public CartSlice() =>
                On<AddItem>((s, a) =>
                {
                    s.Items.Add(a.Item);
                    return s;
                });
        }
        """;
    #endregion

    #region DUCKY004
    private const string Ducky004 = """
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        public static class Startup
        {
            // In a Blazor Server or ASP.NET Core project (Microsoft.NET.Sdk.Web).
            public static void Configure(IServiceCollection services) =>
                services.AddDucky(b => b.Lifetime = ServiceLifetime.Singleton);
        }
        """;
    #endregion

    #region DUCKY005
    private const string Ducky005 = """
        using Ducky;

        public interface ICartEvent;
        public sealed record ItemAdded(string Item) : ICartEvent;
        public sealed record Cart(int Changes);

        public sealed class CartSlice : Slice<Cart>
        {
            protected override Cart Initial => new(0);

            public CartSlice() =>
                On<ICartEvent>(s => s with { Changes = s.Changes + 1 });
        }
        """;
    #endregion

    #region DUCKY006
    private const string Ducky006 = """
        using System.Text.Json.Serialization;
        using Ducky;
        using Ducky.Generated;
        using Microsoft.Extensions.DependencyInjection;

        public sealed record Cart(int Count);
        public sealed record Theme(string Name);

        public sealed class CartSlice : Slice<Cart>
        {
            protected override Cart Initial => new(0);
        }

        public sealed class ThemeSlice : Slice<Theme>
        {
            protected override Theme Initial => new("light");
        }

        [JsonSerializable(typeof(Cart))]
        internal partial class AppJson : JsonSerializerContext;

        public static class Startup
        {
            public static void Configure(IServiceCollection services) =>
                services.AddDucky(b =>
                {
                    b.AddDuckyGenerated_Consumer();
                    b.UseJson(AppJson.Default);
                });
        }
        """;
    #endregion

    #region DUCKY008
    private const string Ducky008 = """
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record Checkout;
        public sealed record CheckedOut;

        public sealed class CheckoutEffect(IStore store) : Effect<Checkout>
        {
            public override Task Handle(Checkout action, EffectContext context, CancellationToken cancellationToken) =>
                store.DispatchAsync(new CheckedOut());
        }
        """;
    #endregion

    #region DUCKY009
    private const string Ducky009 = """
        using Ducky.Blazor;

        public sealed record Cart(int Count);

        public sealed class CartBadge : DuckyComponent
        {
            private int _count;

            protected override void OnParametersSet() => _count = Select(s => s.Get<Cart>().Count);
        }
        """;
    #endregion

    #region DUCKY010
    private const string Ducky010 = """
        using Ducky;

        public sealed record Refresh;

        public sealed class RefreshButton(IStore store)
        {
            public void OnClick() => store.DispatchAsync(new Refresh()).Wait();
        }
        """;
    #endregion

    #region DUCKY011
    private const string Ducky011 = """
        using Ducky;

        public sealed record Clock(int Ticks);
        public sealed record Redraw;

        public sealed class ClockSlice : Slice<Clock>
        {
            protected override Clock Initial => new(0);

            public ClockSlice() => On<Redraw>(s => s with { });
        }
        """;
    #endregion

    #region DUCKY012
    private const string Ducky012 = """
        using Ducky;

        public sealed record Cart(int Count);

        [Persist]
        public sealed class CartSlice : Slice<Cart>
        {
            protected override Cart Initial => new(0);
        }
        """;
    #endregion

    #region DUCKY013
    private const string Ducky013 = """
        using System.Threading;
        using System.Threading.Tasks;
        using Ducky;

        public sealed record Search(string Query, int Page);
        public sealed class SearchKey(string query) { public string Query => query; }

        public sealed class SearchEffect : Effect<Search>
        {
            public override Task Handle(Search action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

            protected override object? ConcurrencyKey(Search action) => new SearchKey(action.Query);
        }
        """;
    #endregion

    #region DUCKY014
    private const string Ducky014 = """
        using Ducky;

        public sealed record Counter(int Value);
        public sealed record Increment;

        public sealed class CounterSlice : Slice<Counter>
        {
            protected override Counter Initial => new(0);

            public CounterSlice()
            {
                On<Increment>(s => s with { Value = s.Value + 1 });
                On<Increment>(s => s with { Value = s.Value + 2 });
            }
        }
        """;
    #endregion

    #region DUCKY015
    private const string Ducky015 = """
        using Ducky;

        public sealed record Counter(int Value);

        public sealed class CounterSlice : Slice<Counter>
        {
            protected override Counter Initial => new(0);
        }

        public sealed class OtherCounterSlice : Slice<Counter>
        {
            protected override Counter Initial => new(10);
        }
        """;
    #endregion

    #region DUCKY016
    private const string Ducky016 = """
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        public sealed record Counter(int Value);

        public sealed class CounterSlice : Slice<Counter>
        {
            protected override Counter Initial => new(0);
        }

        public static class Startup
        {
            public static void Configure(IServiceCollection services) => services.AddDucky(b => { });
        }
        """;
    #endregion

    #region DUCKY020
    private const string Ducky020 = """
        using Ducky;

        [DuckyAction]
        public sealed record Loaded<T>(T Value);
        """;
    #endregion

    #region DUCKY021
    private const string Ducky021 = """
        using Ducky;

        [DuckyAction]
        public sealed record Dispose;
        """;
    #endregion

    #region DUCKY022
    private const string Ducky022 = """
        using Ducky;

        [Persist]
        public sealed record Cart(int Count);
        """;
    #endregion

    #region DUCKY023
    private const string Ducky023 = """
        using Ducky;

        public sealed record Cart(int Count);

        public sealed class CartSlice : Slice<Cart>
        {
            private readonly int _start;

            public CartSlice(int start) => _start = start;

            protected override Cart Initial => new(_start);
        }
        """;
    #endregion

    #region DUCKY024
    private const string Ducky024 = """
        using Ducky;

        namespace Shop.Cart
        {
            [DuckyAction]
            public sealed record Reset(int Id);
        }

        namespace Shop.Search
        {
            [DuckyAction]
            public sealed record Reset(int Id);
        }
        """;
    #endregion

    #region DUCKY025
    private const string Ducky025 = """
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using Ducky;
        using Microsoft.Extensions.DependencyInjection;

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(int))]
        internal partial class CartJson : JsonSerializerContext;

        [JsonSerializable(typeof(string))]
        internal partial class ThemeJson : JsonSerializerContext;

        public static class Startup
        {
            public static void Configure(IServiceCollection services) =>
                services.AddDucky(b => b.UseJson(JsonTypeInfoResolver.Combine(CartJson.Default, ThemeJson.Default)));
        }
        """;
    #endregion

    public static TheoryData<string, string> Examples => new()
    {
        { "DUCKY001", Ducky001 },
        { "DUCKY002", Ducky002 },
        { "DUCKY003", Ducky003 },
        { "DUCKY004", Ducky004 },
        { "DUCKY005", Ducky005 },
        { "DUCKY006", Ducky006 },
        { "DUCKY008", Ducky008 },
        { "DUCKY009", Ducky009 },
        { "DUCKY010", Ducky010 },
        { "DUCKY011", Ducky011 },
        { "DUCKY012", Ducky012 },
        { "DUCKY013", Ducky013 },
        { "DUCKY014", Ducky014 },
        { "DUCKY015", Ducky015 },
        { "DUCKY016", Ducky016 },
        { "DUCKY020", Ducky020 },
        { "DUCKY021", Ducky021 },
        { "DUCKY022", Ducky022 },
        { "DUCKY023", Ducky023 },
        { "DUCKY024", Ducky024 },
        { "DUCKY025", Ducky025 },
    };

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task DocsExample_ReportsExactlyItsId(string id, string source)
    {
        var compilation = DiagnosingHarness.WithReactive(DiagnosingHarness.WithBlazor(GeneratorHarness.Compile(source)));
        var options = new WebProjectOptions();
        CSharpGeneratorDriver.Create(
                [new DispatchExtensionsGenerator().AsSourceGenerator(), new RegistrationGenerator().AsSourceGenerator(), new JsonSourceGenerator().AsSourceGenerator()],
                parseOptions: GeneratorHarness.ParseOptions,
                optionsProvider: options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generated, TestContext.Current.CancellationToken);
        var analyzed = await output.WithAnalyzers(
                [new PurityAnalyzer(), new StructuralAnalyzer(), new SelectInRenderAnalyzer(), new BlockingAnalyzer(), new JsonContextAnalyzer(),
                    new LifetimeAnalyzer(), new RegistrationCallAnalyzer(), new StoreInjectionAnalyzer()],
                new AnalyzerOptions([], options))
            .GetAllDiagnosticsAsync(TestContext.Current.CancellationToken);

        // The example is otherwise valid code: no compiler error, only the one Ducky ID.
        analyzed.Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("DUCKY", StringComparison.Ordinal))
            .Select(d => d.ToString()).ShouldBeEmpty();
        generated.Concat(analyzed).Select(d => d.Id).Where(d => d.StartsWith("DUCKY", StringComparison.Ordinal)).Distinct().ShouldBe([id]);
    }

    // Every descriptor the assembly declares, found by reflection so a new one can't miss its page, link or example.
    private static IEnumerable<DiagnosticDescriptor> AllDescriptors() => typeof(PurityAnalyzer).Assembly.GetTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
        .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);

    public static TheoryData<string> Descriptors => [.. AllDescriptors().Select(d => d.Id).Distinct()];

    [Theory]
    [MemberData(nameof(Descriptors))]
    public void EveryDiagnostic_HasDocsPageAndHelpLink(string id)
    {
        AllDescriptors().Where(d => d.Id == id).ShouldAllBe(d => d.HelpLinkUri == $"{HelpRoot}{id}.md");
        var page = Path.Combine(RepositoryRoot(), "docs", "diagnostics", $"{id}.md");
        File.Exists(page).ShouldBeTrue($"docs/diagnostics/{id}.md is missing");
        File.ReadAllText(page).ShouldContain($"[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#{id})]", Case.Sensitive);
        Examples.Select(row => row.Data.Item1).ShouldContain(id);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "docs", "spec", "SPEC.md")))
        {
            directory = directory.Parent.ShouldNotBeNull("docs/spec/SPEC.md not found above the test output");
        }

        return directory.FullName;
    }

    // The build properties of a Blazor Server or ASP.NET Core project, as the SDK writes them (DUCKY004).
    private sealed class WebProjectOptions : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(new Dictionary<string, string>
        {
            ["build_property.UsingMicrosoftNETSdkWeb"] = "true",
        });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new([]);

            public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) =>
                values.TryGetValue(key, out value);
        }
    }
}
