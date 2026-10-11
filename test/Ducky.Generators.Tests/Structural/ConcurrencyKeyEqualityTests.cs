using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY013 (SPEC §5.5, §16.2): a ConcurrencyKey override that returns a class or array without value equality (no
// Equals override in its hierarchy, so not a record either) never relates two runs. Reported on the returned
// expression; the argument is its type.
public sealed class ConcurrencyKeyEqualityTests
{
    private const string Types = """
        public sealed class Plain(int id) { public int Id => id; }
        public class Equal { public override bool Equals(object? o) => o is Equal; public override int GetHashCode() => 0; }
        public sealed class DerivedEqual : Equal;
        public interface IKey;
        public sealed record class RecordKey(int Id);
        public readonly record struct StructKey(int Id);

        """;

    private static string Effect(string member) => Prelude + Types + $$"""
        public sealed class E : Effect<Hit>
        {
            private readonly IKey? _key = null;
            public override Task Handle(Hit a, EffectContext c, CancellationToken t) => Task.CompletedTask;
            {{member}}
        }
        """;

    public static TheoryData<string, string> Reported => new()
    {
        { Effect("protected override object? ConcurrencyKey(Hit a) => {|#0:new Plain(a.Id)|};"), "Plain" },
        { Effect("protected override object? ConcurrencyKey(Hit a) { if (a.Id > 0) { return a.Id; } return {|#0:(object)new Plain(1)|}; }"), "Plain" },
        { Effect("protected override object? ConcurrencyKey(Hit a) => {|#0:new[] { a.Id }|};"), "int[]" },
        // A conditional with a natural type keeps it: the conversion to object wraps a Plain.
        { Effect("protected override object? ConcurrencyKey(Hit a) => {|#0:a.Id > 0 ? new Plain(1) : null|};"), "Plain" },
        // An override of an override still overrides Effect<T>.ConcurrencyKey.
        {
            Prelude + Types + """
                public class E1 : Effect<Hit> { public override Task Handle(Hit a, EffectContext c, CancellationToken t) => Task.CompletedTask; protected override object? ConcurrencyKey(Hit a) => a.Id; }
                public sealed class E2 : E1 { protected override object? ConcurrencyKey(Hit a) => {|#0:new Plain(2)|}; }
                """,
            "Plain"
        },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky013_KeyWithoutValueEquality_Reported(string source, string type) =>
        Verifier.VerifyAsync(source, Verifier.Diagnostic(StructuralDiagnostics.ConcurrencyKeyEquality).WithLocation(0).WithArguments(type));

    public static TheoryData<string> Silent => new()
    {
        Effect("protected override object? ConcurrencyKey(Hit a) => a;"),
        Effect("protected override object? ConcurrencyKey(Hit a) => a.Id;"),
        Effect("protected override object? ConcurrencyKey(Hit a) => a.Id.ToString();"),
        Effect("protected override object? ConcurrencyKey(Hit a) => (a.Id, a.Id);"),
        Effect("protected override object? ConcurrencyKey(Hit a) => new RecordKey(a.Id);"),
        Effect("protected override object? ConcurrencyKey(Hit a) => new StructKey(a.Id);"),
        Effect("protected override object? ConcurrencyKey(Hit a) => new DerivedEqual();"),
        Effect("protected override object? ConcurrencyKey(Hit a) => new { a.Id };"),
        Effect("protected override object? ConcurrencyKey(Hit a) => null;"),
        // Unknown runtime types: an interface and object itself.
        Effect("protected override object? ConcurrencyKey(Hit a) => _key;"),
        Effect("protected override object? ConcurrencyKey(Hit a) => new object();"),
        // No natural type: the conditional or switch is target-typed to object.
        Effect("protected override object? ConcurrencyKey(Hit a) => a.Id > 0 ? a.Id : null;"),
        Effect("protected override object? ConcurrencyKey(Hit a) => a.Id switch { 1 => a.Id, _ => \"x\" };"),
        // A nested lambda or local function returns its own value, not the key.
        Effect("protected override object? ConcurrencyKey(Hit a) { Func<object> f = () => new Plain(1); return a.Id; }"),
        Effect("protected override object? ConcurrencyKey(Hit a) { object L() => new Plain(2); return a.Id; }"),
        // A lambda in a field or property initializer: its containing symbol is not a method.
        Prelude + Types + "public sealed class F { private readonly Func<object> f = () => \"x\"; }",
        Prelude + Types + "public sealed class F { public Func<IEnumerable<int>> G { get; } = () => new List<int>(); }",
        // Not Ducky's ConcurrencyKey.
        Prelude + Types + "public sealed class Other { private object ConcurrencyKey(Hit a) => new Plain(a.Id); public object Key(Hit a) => new Plain(a.Id); }",
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky013_KeyWithValueEquality_NotReported(string source) => Verifier.VerifyAsync(source);
}
