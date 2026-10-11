using Ducky.Generators.Analyzers.Purity;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// DUCKY001 (SPEC §16.2), reducer and projector arms: each listed non-deterministic member is reported at its use, with
// the member and the arm as arguments.
public sealed class NonDeterminismTests
{
    public static TheoryData<string, string, string> Reported
    {
        get
        {
            var rows = new TheoryData<string, string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                rows.Add(arm, "s with { Count = {|#0:DateTime.Now|}.Second }", "DateTime.Now");
                rows.Add(arm, "s with { Count = {|#0:DateTime.UtcNow|}.Second }", "DateTime.UtcNow");
                rows.Add(arm, "s with { Count = {|#0:DateTime.Today|}.Day }", "DateTime.Today");
                rows.Add(arm, "s with { Count = {|#0:DateTimeOffset.Now|}.Second }", "DateTimeOffset.Now");
                rows.Add(arm, "s with { Count = {|#0:DateTimeOffset.UtcNow|}.Second }", "DateTimeOffset.UtcNow");
                rows.Add(arm, "s with { Count = {|#0:Guid.NewGuid()|}.GetHashCode() }", "Guid.NewGuid");
                rows.Add(arm, "s with { Count = Recipes.Use({|#0:Guid.NewGuid|}) }", "Guid.NewGuid");
                rows.Add(arm, "s with { Count = {|#0:new Random()|}.GetHashCode() }", "new Random");
                rows.Add(arm, "s with { Count = {|#0:Environment.TickCount|} }", "Environment.TickCount");
                rows.Add(arm, "s with { Count = (int){|#0:Environment.TickCount64|} }", "Environment.TickCount64");
                rows.Add(arm, "s with { Count = (int){|#0:Stopwatch.GetTimestamp()|} }", "Stopwatch.GetTimestamp");
                rows.Add(arm, "s with { Count = {|#0:time.GetUtcNow()|}.Second }", "TimeProvider.GetUtcNow");
                rows.Add(arm, "s with { Count = {|#0:fixedTime.GetUtcNow()|}.Second }", "FixedTime.GetUtcNow");
                rows.Add(arm, "s with { Count = s.Items.Select(_ => {|#0:DateTime.Now|}.Second).Sum() }", "DateTime.Now");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky001_NonDeterministicMember_Reported(string arm, string body, string member) =>
        Verifier.VerifyAsync(Host(arm, body), Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments(member, arm));

    // Every member of Random is non-deterministic, so the shared instance and the call on it are two uses.
    [Theory]
    [MemberData(nameof(Arms), MemberType = typeof(PuritySources))]
    public Task Ducky001_EveryRandomMember_Reported(string arm) =>
        Verifier.VerifyAsync(
            Host(arm, "s with { Count = {|#1:{|#0:Random.Shared|}.Next()|} }"),
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments("Random.Shared", arm),
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(1).WithArguments("Random.Next", arm));

    public static TheoryData<string, string> Silent
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                rows.Add(arm, "s with { Count = DateTime.MinValue.Second + DateTime.Parse(\"2026-01-01\").Day }");
                rows.Add(arm, "s with { Count = DateTimeOffset.MinValue.Second + Guid.Empty.GetHashCode() }");
                rows.Add(arm, "s with { Count = Environment.NewLine.Length + (int)TimeSpan.FromSeconds(1).TotalSeconds }");
                rows.Add(arm, "s with { Count = time.LocalTimeZone.BaseUtcOffset.Hours }");
                // nameof evaluates nothing.
                rows.Add(arm, "s with { Count = nameof(DateTime.Now).Length + nameof(Stopwatch.Frequency).Length }");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky001_DeterministicMember_NotReported(string arm, string body) => Verifier.VerifyAsync(Host(arm, body));

    // The projector is Define's last parameter wherever named arguments put it; the input lambda is not analysed.
    [Fact]
    public Task Ducky001_ReorderedNamedArguments_ProjectorReported() =>
        Verifier.VerifyAsync(
            Prelude + """

            public static class Host
            {
                public static object Make() => Selector.Define(projector: c => {|#0:DateTime.Now|}.Second + c, in1: x => x.Get<Bag>().Count);
            }
            """,
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments("DateTime.Now", Projector));

    // Only the reducer and projector lambdas are pure: a constructor body, an effect handler, a method group, a stored
    // delegate and a lookalike On<T> or Selector.Define outside Ducky are not analysed.
    [Fact]
    public Task Ducky001_OutsideReducerAndProjector_NotReported() =>
        Verifier.VerifyAsync(Prelude + """

            public sealed record Load;
            public sealed class Host : Slice<Bag>
            {
                protected override Bag Initial => new(DateTime.Now.Second, [], [], [], [], new());

                public Host()
                {
                    var now = DateTime.Now;
                    On<Hit>(Reduce);
                    Func<Bag, Hit, Bag> stored = (s, a) => s with { Count = DateTime.Now.Second };
                    On<Hit>(stored);
                }

                private static Bag Reduce(Bag s, Hit a) => s with { Count = DateTime.Now.Second };
            }

            public sealed class Loader : EffectGroup
            {
                public Loader() => On<Load>((_, _, _) => Task.FromResult(DateTime.Now));
            }

            public sealed class Lookalike
            {
                public void On<T>(Func<Bag, T, Bag> reducer) { }
                public Lookalike() => On<Hit>((s, a) => s with { Count = DateTime.Now.Second });
            }

            namespace Other
            {
                public static class Selector
                {
                    public static int Define<T>(Func<int, T> projector) => 0;
                    public static int Use() => Define(x => DateTime.Now);
                }
            }
            """);

    [Fact]
    public Task Ducky001_GeneratedCode_NotReported() =>
        Verifier.VerifyAsync("// <auto-generated/>\n" + Host(Reducer, "s with { Count = DateTime.Now.Second }"));
}
