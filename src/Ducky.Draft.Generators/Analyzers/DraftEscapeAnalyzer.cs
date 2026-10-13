using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Draft.Generators;

/// <summary>
/// DUCKY106 (SPEC §16.2): a <c>Produce</c> recipe lets a draft escape the lambda. A draft reached from the recipe's
/// parameter (the draft itself, a nested draft, a collection draft or an element draft: anything implementing
/// <c>Ducky.Draft.IRevocable</c>), or a lambda or local function that reads one, is assigned to a field, an array element
/// or a property of an object the recipe did not create, or to a variable captured from outside the recipe. <c>Produce</c> revokes the draft when it
/// returns, so any later use throws.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DraftEscapeAnalyzer : DiagnosticAnalyzer
{
    private const string Produce = "Produce";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DraftDiagnostics.Escape];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // A component's @code block compiles to generated C# (*.razor.g.cs); the Draft generator emits no recipe.
        // Stryker disable once Statement : equivalent, Analyze | ReportDiagnostics is also Roslyn's default when unconfigured (RS1025 asks to state it)
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Without the Ducky.Draft runtime both are null and match no type, so nothing is reported.
            var draft = start.Compilation.GetTypeByMetadataName("Ducky.Draft.IDraft`1");
            var revocable = start.Compilation.GetTypeByMetadataName("Ducky.Draft.IRevocable");
            start.RegisterOperationAction(c => Analyze(c, draft, revocable), OperationKind.Invocation);
        });
    }

    // Pattern variables live only in case labels, so no mutant of a condition can leave one unassigned.
    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol? draft, INamedTypeSymbol? revocable)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != Produce)
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            switch (argument.Value)
            {
                case IDelegateCreationOperation { Target: IAnonymousFunctionOperation recipe }
                    when recipe.Symbol.Parameters.Any(p => p.Type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, draft))):
                    Inspect(context, recipe, revocable);
                    break;
            }
        }
    }

    // `Produce` takes an Action, so a draft leaves the recipe only through a store that outlives it.
    // ponytail: assignments only; a draft passed to a method (`list.Add(d)`), deconstructed into a field, bound by a
    // pattern (`d.Child is var c`) or subscribed as an event handler escapes unseen, which needs data flow.
    private static void Inspect(OperationAnalysisContext context, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable)
    {
        foreach (var operation in recipe.Body.Descendants())
        {
            switch (operation)
            {
                case IAssignmentOperation assignment when Escapes(assignment.Target, recipe, revocable) && Carries(WithoutConversions(assignment.Value), recipe, revocable):
                    context.ReportDiagnostic(Diagnostic.Create(
                        DraftDiagnostics.Escape, operation.Syntax.GetLocation(), WithoutConversions(assignment.Value).Syntax.ToString()));
                    break;
            }
        }
    }

    // Where a stored value outlives the recipe: a property of anything but a draft of the tree (`d.Child = d.Child`,
    // `d.Items[0] = ...` stay inside; `_holders[d.Child.Value].Draft = d` does not: its receiver only reads the draft) or of
    // an object the recipe made, a field or an array element of anything the recipe did not make, a local or parameter
    // declared outside the recipe (a captured variable), and any other target: a dynamic member. A deconstruction's target
    // is a tuple, so its value (a tuple) carries no draft.
    private static bool Escapes(IOperation target, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) => target switch
    {
        IPropertyReferenceOperation property => !Drafted(property.Instance, recipe, revocable) && !Made(property.Instance, recipe, revocable),
        IFieldReferenceOperation field => !Made(field.Instance, recipe, revocable),
        ILocalReferenceOperation local => !DeclaredIn(local.Local, recipe),
        IParameterReferenceOperation parameter => !DeclaredIn(parameter.Parameter, recipe),
        IArrayElementReferenceOperation element => !Made(element.ArrayReference, recipe, revocable),
        IDiscardOperation => false,
        _ => true,
    };

    // A receiver the recipe made: the object an initializer fills (`new Holder { Draft = d }`, `new { D = d }`), or, on
    // positive evidence only, a local of a declaration (no foreach, pattern, out or deconstruction variable) never passed
    // by ref or out, aliased by a ref local or deconstructed into, whose every value Sources sees is a new object, array or
    // collection (`var h = new Holder(); h.Draft = d;`, `var a = new Todo.Draft[1];`): definite assignment leaves it no
    // other write. A local the recipe also sets from elsewhere, or a nested member initializer (`Inner = { Draft = d }`,
    // filling an object found elsewhere), is not.
    // ponytail: a made object that is itself stored outside later (`var h = new Holder { Draft = d }; _h = h;`) escapes
    // unseen, like a draft passed to a method.
    private static bool Made(IOperation? receiver, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) => receiver switch
    {
        IConversionOperation conversion => Made(conversion.Operand, recipe, revocable),
        ILocalReferenceOperation local => DeclaredIn(local.Local, recipe)
            && local.Local.DeclaringSyntaxReferences[0].GetSyntax() is VariableDeclaratorSyntax
            && Sources(local.Local, recipe).All(s => s is null || Fresh(s))
            && !recipe.Body.Descendants().OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, local.Local) && Rebound(r)),
        IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver } filled => Filled(filled.Parent!.Parent!.Parent, recipe, revocable),
        _ => false,
    };

    private static bool Fresh(IOperation source) =>
        WithoutConversions(source) is IObjectCreationOperation or IArrayCreationOperation or ICollectionExpressionOperation;

    // The initializer holding a member assignment (receiver -> member -> assignment -> here: a target always has those), for
    // a new object that is not itself stored where it escapes (`_any = new Holder { Draft = d }` reports `Draft = d`).
    private static bool Filled(IOperation? initializer, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) => initializer switch
    {
        IAnonymousObjectCreationOperation creation => !Stored(creation, recipe, revocable),
        IObjectOrCollectionInitializerOperation { Parent: IObjectCreationOperation creation } => !Stored(creation, recipe, revocable),
        _ => false,
    };

    private static bool Stored(IOperation value, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) => value.Parent switch
    {
        IConversionOperation conversion => Stored(conversion, recipe, revocable),
        IAssignmentOperation assignment => Escapes(assignment.Target, recipe, revocable),
        _ => false,
    };

    // The stored value is a draft reached from the recipe parameter, or a lambda or method group that reads one.
    private static bool Carries(IOperation value, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) => value switch
    {
        IDelegateCreationOperation closure => ReachedFrom(closure.Target, recipe),
        _ => Drafted(value, recipe, revocable),
    };

    // A draft (its type implements IRevocable) reached from the recipe parameter; null (a static member's receiver) is none.
    // Only what the value holds counts, not an index or key that picks it (`m[d.Title]` of a recipe-local map).
    private static bool Drafted(IOperation? operation, IAnonymousFunctionOperation recipe, INamedTypeSymbol? revocable) =>
        operation?.Type?.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, revocable)) == true
        && ReachedFrom(operation, recipe, new HashSet<ISymbol>(SymbolEqualityComparer.Default), held: true);

    private static IOperation WithoutConversions(IOperation operation) => operation switch
    {
        IConversionOperation conversion => WithoutConversions(conversion.Operand),
        _ => operation,
    };

    // The operation reads the recipe's parameter (`d`, `d.Child`, `d.Items[0]`), a local the recipe writes from such a
    // read (`var c = d.Child;`, `foreach (var c in d.Items)`), or a local function of the recipe that does.
    // A lambda (or local function body) escapes on any read, keys included; a value (`held`) only through what it holds.
    private static bool ReachedFrom(IOperation? operation, IAnonymousFunctionOperation recipe) =>
        ReachedFrom(operation, recipe, new HashSet<ISymbol>(SymbolEqualityComparer.Default), held: false);

    // `seen` stops a cycle (`a = b; b = a;`, a recursive local function): a symbol already followed adds nothing.
    // ponytail: rescans the recipe per local or local function followed, quadratic in the recipe's size.
    private static bool ReachedFrom(IOperation? operation, IAnonymousFunctionOperation recipe, HashSet<ISymbol> seen, bool held) =>
        operation is not null && (held ? Held(operation) : operation.DescendantsAndSelf()).Any(o => o switch
        {
            IParameterReferenceOperation read => SymbolEqualityComparer.Default.Equals(read.Parameter.ContainingSymbol, recipe.Symbol),
            ILocalReferenceOperation read => seen.Add(read.Local) && Sources(read.Local, recipe).Any(s => ReachedFrom(s, recipe, seen, held)),
            IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction } call => Follow(call.TargetMethod, recipe, seen),
            IMethodReferenceOperation { Method.MethodKind: MethodKind.LocalFunction } group => Follow(group.Method, recipe, seen),
            _ => false,
        });

    // The operation and its descendants, minus indexer arguments and array indices: a key picks a value, it is not held.
    // A method's arguments stay: it may return one (`d.Items.First()`).
    private static IEnumerable<IOperation> Held(IOperation operation) =>
        new[] { operation }.Concat(operation.ChildOperations.Where(child => !Key(child)).SelectMany(Held));

    private static bool Key(IOperation child) => child switch
    {
        IArgumentOperation { Parent: IPropertyReferenceOperation } => true,
        { Parent: IArrayElementReferenceOperation element } => element.ArrayReference != child,
        _ => false,
    };

    private static bool Follow(IMethodSymbol function, IAnonymousFunctionOperation recipe, HashSet<ISymbol> seen) =>
        seen.Add(function) && recipe.Body.Descendants().OfType<ILocalFunctionOperation>()
            .Any(f => SymbolEqualityComparer.Default.Equals(f.Symbol, function) && ReachedFrom(f.Body, recipe, seen, held: false));

    // What the recipe writes a local from: its initializer, an assignment, or the collection a foreach walks.
    private static IEnumerable<IOperation?> Sources(ILocalSymbol local, IAnonymousFunctionOperation recipe) =>
        recipe.Body.Descendants().Select(o => o switch
        {
            IVariableDeclaratorOperation declarator when SymbolEqualityComparer.Default.Equals(declarator.Symbol, local) => declarator.Initializer?.Value,
            IAssignmentOperation { Target: ILocalReferenceOperation target } assignment when SymbolEqualityComparer.Default.Equals(target.Local, local) => assignment.Value,
            IForEachLoopOperation loop when loop.Locals.Contains(local, SymbolEqualityComparer.Default) => loop.Collection,
            _ => null,
        });

    // Declared in the recipe or in a lambda or local function nested in it. By containment, not by source span: a
    // synthesized parameter (top-level `args`) has no location. No pattern variable, so no mutant breaks definite
    // assignment.
    private static bool DeclaredIn(ISymbol? symbol, IAnonymousFunctionOperation recipe) => symbol switch
    {
        null => false,
        _ when SymbolEqualityComparer.Default.Equals(symbol, recipe.Symbol) => true,
        _ => DeclaredIn(symbol.ContainingSymbol, recipe),
    };

    // A write of a local that Sources does not see: a ref or out argument, a ref alias (`ref var r = ref h;`,
    // `r = ref h;`, `ref c ? ref h : ref g`), or a deconstruction target (`(h, _) = …`). Parameter is null only for
    // `__arglist`, whose values are no direct child of the argument.
    private static bool Rebound(IOperation reference) => reference.Parent switch
    {
        IArgumentOperation argument => argument.Parameter!.RefKind is RefKind.Ref or RefKind.Out,
        _ when reference.Syntax.Parent is RefExpressionSyntax => true,
        ITupleOperation tuple => Rebound(tuple),
        IDeconstructionAssignmentOperation deconstruction => deconstruction.Target == reference,
        _ => false,
    };
}
