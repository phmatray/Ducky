namespace Ducky;

/// <summary>
/// An effect group over the cases of the C# union <typeparamref name="TAction"/>: one <see cref="Handle"/> receives each
/// case wrapped in the union, so a <c>switch</c> over it is checked for exhaustiveness (ADR-0051). Each case is its own
/// effect with <see cref="Concurrency.Merge"/> and one global slot; for a policy or key per case, derive from
/// <see cref="EffectGroup"/> and call <c>On&lt;TCase&gt;</c>.
/// </summary>
/// <typeparam name="TAction">The union of the actions the group handles.</typeparam>
public abstract class EffectGroup<TAction> : EffectGroup
    where TAction : notnull
{
    /// <summary>Initializes a new instance of the <see cref="EffectGroup{TAction}"/> class and registers its cases.</summary>
#pragma warning disable CA2214 // justification: by design (ADR-0051), the hook only calls On<T>, whose table the base constructor already built
    protected EffectGroup() => RegisterCases();
#pragma warning restore CA2214

    /// <summary>
    /// Handles one action of the union, like <see cref="Effect{TAction}.Handle"/>: an <see cref="OperationCanceledException"/>
    /// for <paramref name="cancellationToken"/> is cancellation; any other exception dispatches <see cref="EffectFailed"/>.
    /// </summary>
    /// <param name="action">The dispatched case, wrapped in the union.</param>
    /// <param name="context">The live state, the action's context, the clock, and dispatch.</param>
    /// <param name="cancellationToken">Cancelled when the store is disposed.</param>
    /// <returns>A task that completes when the run ends.</returns>
#pragma warning disable VSTHRD200 // justification: Handle is the normative name of SPEC §5.5
    public abstract Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken);
#pragma warning restore VSTHRD200

    /// <summary>
    /// Registers each case of <typeparamref name="TAction"/> with <c>On&lt;Case&gt;</c>, called once by the constructor. The
    /// Ducky generator overrides it in the group's <c>partial</c> declaration; without the generator, override it by hand.
    /// </summary>
    protected virtual void RegisterCases()
    {
    }
}
