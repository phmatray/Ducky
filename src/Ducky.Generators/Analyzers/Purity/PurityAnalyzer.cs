using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Analyzers.Purity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY001–003 (SPEC §12, §16.2): an <c>On&lt;T&gt;</c> reducer lambda, a <c>SliceStore.Set</c>/<c>SetAsync</c> mutator
/// lambda and a <c>Selector.Define</c> projector lambda must be pure. Method groups and generated code are not analysed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PurityAnalyzer : DiagnosticAnalyzer
{
    private const string Parameter = "a parameter";
    private const string Captured = "a captured variable";
    private const string Field = "a field";
    private const string Mutator = "mutator";

    // (type metadata name, member name or null for every member, rule). A type matches itself, a derived type and an
    // implementing type, so a FakeTimeProvider override or an IJSInProcessRuntime call is caught too.
    private static readonly ImmutableArray<(string Type, string? Member, DiagnosticDescriptor Rule)> _typeRules =
    [
        ("System.DateTime", "Now", PurityDiagnostics.NonDeterministic),
        ("System.DateTime", "UtcNow", PurityDiagnostics.NonDeterministic),
        ("System.DateTime", "Today", PurityDiagnostics.NonDeterministic),
        ("System.DateTimeOffset", "Now", PurityDiagnostics.NonDeterministic),
        ("System.DateTimeOffset", "UtcNow", PurityDiagnostics.NonDeterministic),
        ("System.Guid", "NewGuid", PurityDiagnostics.NonDeterministic),
        ("System.Random", null, PurityDiagnostics.NonDeterministic),
        ("System.Environment", "TickCount", PurityDiagnostics.NonDeterministic),
        ("System.Environment", "TickCount64", PurityDiagnostics.NonDeterministic),
        ("System.Diagnostics.Stopwatch", null, PurityDiagnostics.NonDeterministic),
        ("System.TimeProvider", "GetUtcNow", PurityDiagnostics.NonDeterministic),
        ("System.Net.Http.HttpClient", null, PurityDiagnostics.Io),
        ("Microsoft.JSInterop.IJSRuntime", null, PurityDiagnostics.Io),
        ("System.Console", null, PurityDiagnostics.Io),
        ("System.Threading.Tasks.Task`1", "Result", PurityDiagnostics.Io),
        ("System.Threading.Tasks.ValueTask`1", "Result", PurityDiagnostics.Io),
        ("System.Threading.Tasks.Task", "Wait", PurityDiagnostics.Io),
    ];

    // `System.IO.*`, and GetResult on every awaiter (TaskAwaiter, ConfiguredTaskAwaiter, ValueTaskAwaiter…).
    private const string IoNamespace = "System.IO";
    private const string AwaiterNamespace = "System.Runtime.CompilerServices";
    private const string GetResult = "GetResult";

    // Known mutators of the mutable collections (List<T>.Add, …): a call on the parameters' graph is DUCKY003.
    private static readonly ImmutableHashSet<string> _mutatorNamespaces =
    [
        "System.Collections", "System.Collections.Generic", "System.Collections.ObjectModel", "System.Collections.Concurrent",
    ];

    private static readonly ImmutableHashSet<string> _mutators =
    [
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll", "RemoveRange", "RemoveWhere", "Clear",
        "Sort", "Reverse", "UnionWith", "IntersectWith", "ExceptWith", "SymmetricExceptWith", "Enqueue", "Dequeue",
        "TryDequeue", "Push", "Pop", "TryPop", "TryAdd", "TryRemove", "AddOrUpdate", "GetOrAdd", "AddFirst", "AddLast",
        "RemoveFirst", "RemoveLast",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [PurityDiagnostics.NonDeterministic, PurityDiagnostics.Io, PurityDiagnostics.Mutation];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // An unresolved type is null and never matches, so a compilation without Ducky or JSInterop needs no branch.
            var compilation = start.Compilation;
            var slice = compilation.GetTypeByMetadataName("Ducky.Slice`1");
            var sliceStore = compilation.GetTypeByMetadataName("Ducky.SliceStore`1");
            var selector = compilation.GetTypeByMetadataName("Ducky.Selector");
            var rules = _typeRules.Select(r => (Type: compilation.GetTypeByMetadataName(r.Type), r.Member, r.Rule)).ToImmutableArray();
            start.RegisterOperationAction(
                c => AnalyzeInvocation(c, (IInvocationOperation)c.Operation, slice, sliceStore, selector, rules), OperationKind.Invocation);
        });
    }

    // Pattern variables live only in case labels: on an `||`, a negated condition or a ternary, a mutant could leave
    // one unassigned, and Stryker would roll the whole method back (Safe Mode).
    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        INamedTypeSymbol? slice,
        INamedTypeSymbol? sliceStore,
        INamedTypeSymbol? selector,
        ImmutableArray<(INamedTypeSymbol? Type, string? Member, DiagnosticDescriptor Rule)> rules)
    {
        // On<T>, Set/SetAsync and Define are the only members a consumer can invoke on Slice<TState>,
        // SliceStore<TState> and Selector. Every On<T> and Define overload takes the reducer or projector as its last
        // parameter, Set and SetAsync take the mutator first (the CallerMemberName name follows). Arguments are in source
        // order, so a named argument can move it: it is found by parameter, not by position.
        var method = invocation.TargetMethod;
        var type = method.ContainingType.OriginalDefinition;
        var arm = Is(type, slice) ? "reducer" : Is(type, sliceStore) ? Mutator : Is(type, selector) ? "projector" : null;
        if (arm is null)
        {
            return;
        }

        // A loop, not FirstOrDefault: exactly one argument matches, so a First mutant would be equivalent. C# has no
        // __arglist here, so every argument has a parameter.
        var target = arm == Mutator ? 0 : method.Parameters.Length - 1;
        IAnonymousFunctionOperation? lambda = null;
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter!.Ordinal == target)
            {
                lambda = (argument.Value as IDelegateCreationOperation)?.Target as IAnonymousFunctionOperation;
            }
        }

        if (lambda is null)
        {
            return;
        }

        foreach (var operation in Walk(lambda.Body))
        {
            switch (operation)
            {
                case IAwaitOperation:
                    context.ReportDiagnostic(Diagnostic.Create(PurityDiagnostics.Io, operation.Syntax.GetLocation(), "await", arm));
                    break;
                // await foreach and await using await without an IAwaitOperation: reported at their `await` keyword.
                case IForEachLoopOperation { IsAsynchronous: true }:
                case IUsingOperation { IsAsynchronous: true }:
                case IUsingDeclarationOperation { IsAsynchronous: true }:
                    context.ReportDiagnostic(Diagnostic.Create(PurityDiagnostics.Io, operation.Syntax.GetFirstToken().GetLocation(), "await", arm));
                    break;
                case IInvocationOperation call:
                    Check(context, call, call.TargetMethod, rules, arm);
                    CheckMutator(context, call, lambda, arm);
                    break;
                case IMemberReferenceOperation reference:
                    Check(context, reference, reference.Member, rules, arm);
                    break;
                case IObjectCreationOperation objectCreation:
                    // C# always binds a constructor here: a NoPIA creation is OperationKind.None, an unresolved one invalid.
                    Check(context, objectCreation, objectCreation.Constructor!, rules, arm);
                    break;
                case IAssignmentOperation assignment:
                    CheckWrite(context, assignment, assignment.Target, lambda, arm);
                    break;
                case IIncrementOrDecrementOperation increment:
                    CheckWrite(context, increment, increment.Target, lambda, arm);
                    break;
            }
        }
    }

    // DUCKY001/002 on a use of a listed member. An extension method counts as a member of its `this` type, so
    // js.InvokeVoidAsync(…) and http.GetFromJsonAsync(…) are IJSRuntime and HttpClient uses.
    private static void Check(
        OperationAnalysisContext context,
        IOperation operation,
        ISymbol member,
        ImmutableArray<(INamedTypeSymbol? Type, string? Member, DiagnosticDescriptor Rule)> rules,
        string arm)
    {
        var method = member as IMethodSymbol;
        var type = method?.IsExtensionMethod == true ? method.Parameters[0].Type : member.ContainingType;
        // An array or pointer `this` type (arr.AsSpan()) has no namespace.
        var ns = type.ContainingNamespace?.ToDisplayString();
        var rule = rules.FirstOrDefault(r => (r.Member is null || r.Member == member.Name) && Inherits(type, r.Type)).Rule
            ?? (ns == IoNamespace || ns?.StartsWith(IoNamespace + ".", StringComparison.Ordinal) == true
                || (member.Name == GetResult && ns == AwaiterNamespace)
                ? PurityDiagnostics.Io
                : null);
        if (rule is not null)
        {
            var name = member is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "new " + type.Name : type.Name + "." + member.Name;
            context.ReportDiagnostic(Diagnostic.Create(rule, operation.Syntax.GetLocation(), name, arm));
        }
    }

    // DUCKY003 on a known collection mutator whose receiver is the lambda's parameters' graph.
    private static void CheckMutator(OperationAnalysisContext context, IInvocationOperation call, IAnonymousFunctionOperation lambda, string arm)
    {
        var method = call.TargetMethod;
        if (call.Instance is not null
            && _mutators.Contains(method.Name)
            && _mutatorNamespaces.Contains(method.ContainingType.ContainingNamespace.ToDisplayString())
            && Owner(Root(call.Instance), lambda) == Parameter)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PurityDiagnostics.Mutation, call.Syntax.GetLocation(), method.ContainingType.Name + "." + method.Name, Parameter, arm));
        }
    }

    // DUCKY003 on a write. Rebinding the lambda's own parameter replaces no shared value, so only a member of it counts.
    private static void CheckWrite(
        OperationAnalysisContext context, IOperation write, IOperation target, IAnonymousFunctionOperation lambda, string arm)
    {
        switch (target)
        {
            // (s.Box.Value, captured) = (1, 2): each element is a write, nested tuples included. A discard or a
            // declaration roots at itself and owns nothing.
            case ITupleOperation tuple:
                foreach (var element in tuple.Elements)
                {
                    CheckWrite(context, write, element, lambda, arm);
                }

                break;
            default:
                var root = Root(target);
                var owner = Owner(root, lambda);
                if (owner is not null && (owner != Parameter || root != target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(PurityDiagnostics.Mutation, write.Syntax.GetLocation(), target.Syntax.ToString(), owner, arm));
                }

                break;
        }
    }

    // The variable, `this` or static member a reference hangs off: s.Box.Value → s, ((Box)s.Box).Value → s, _count → this.
    // ponytail: no data flow, so an alias (var items = s.Items; items.Add(1)) and a call result are not followed.
    private static IOperation Root(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IMemberReferenceOperation { Instance: { } instance }:
                    operation = instance;
                    break;
                case IArrayElementReferenceOperation element:
                    operation = element.ArrayReference;
                    break;
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    break;
                default:
                    return operation;
            }
        }
    }

    // What a root belongs to: the lambda's own parameters (the state and action, or the projector inputs), a captured
    // local or parameter, or a field (`this` or a static member). Locals and parameters declared inside the lambda,
    // the parameters of a nested recipe lambda (s.Produce(d => …)) included, and initializer receivers are not state.
    private static string? Owner(IOperation root, IAnonymousFunctionOperation lambda) => root switch
    {
        IParameterReferenceOperation p when SymbolEqualityComparer.Default.Equals(p.Parameter.ContainingSymbol, lambda.Symbol) => Parameter,
        IParameterReferenceOperation p => Declared(p.Parameter, lambda),
        ILocalReferenceOperation l => Declared(l.Local, lambda),
        IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } => Field,
        IMemberReferenceOperation => Field,
        _ => null,
    };

    private static string? Declared(ISymbol symbol, IAnonymousFunctionOperation lambda)
    {
        // By containing symbol, not by source span: a synthesized parameter (the top-level `args`) has no location,
        // and a nested lambda's or local function's locals belong to the lambda too.
        for (var container = symbol.ContainingSymbol; container is not null; container = container.ContainingSymbol)
        {
            if (Is(container, lambda.Symbol))
            {
                return null;
            }
        }

        return Captured;
    }

    private static bool Is(ISymbol symbol, ISymbol? other) => SymbolEqualityComparer.Default.Equals(symbol, other);

    private static bool Inherits(ITypeSymbol type, INamedTypeSymbol? target)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (Is(current.OriginalDefinition, target))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(i => Is(i.OriginalDefinition, target));
    }

    // Every operation below the root, nested lambdas included, except the operand of a nameof: nothing there runs.
    private static IEnumerable<IOperation> Walk(IOperation root)
    {
        var pending = new Stack<IOperation>(root.ChildOperations);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            if (operation is INameOfOperation)
            {
                continue;
            }

            yield return operation;
            foreach (var child in operation.ChildOperations)
            {
                pending.Push(child);
            }
        }
    }
}
