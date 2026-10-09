using System.ComponentModel;

// SPEC §22.1, ADR-0038: member-less error-level tombstones for the Ducky 1.0.292 base types that user code derives
// from, implements or calls statically, in their published namespace. Interfaces even for 1.x classes: nothing to
// construct, cover or test. Removed in 3.0.
namespace Ducky;

/// <summary>The help link of every DUCKYM diagnostic.</summary>
internal static class Tombstone
{
    /// <summary>The <see cref="ObsoleteAttribute.UrlFormat"/> of every tombstone.</summary>
    internal const string UrlFormat = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{0}.md";
}

#pragma warning disable CA1715 // justification: tombstones keep the published 1.x names of 1.x classes (SPEC §22.1)
/// <summary>Removed in Ducky 2.0 (DUCKYM001): any object is a state.</summary>
[Obsolete("Ducky 2.0 removed IState: delete the base type, any object is a state.", error: true, DiagnosticId = "DUCKYM001", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IState;

/// <summary>Removed in Ducky 2.0 (DUCKYM001): any object is an action.</summary>
[Obsolete("Ducky 2.0 removed IKeyedAction: delete the base type, any object is an action.", error: true, DiagnosticId = "DUCKYM001", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IKeyedAction;

/// <summary>Removed in Ducky 2.0 (DUCKYM002): derive from <see cref="Slice{TState}"/>.</summary>
/// <typeparam name="TState">The slice state.</typeparam>
[Obsolete("Ducky 2.0 removed SliceReducers<TState>: write `sealed class X : Slice<TState>` with `protected override TState Initial =>`.", error: true, DiagnosticId = "DUCKYM002", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface SliceReducers<TState>;

/// <summary>Removed in Ducky 2.0 (DUCKYM002): derive from <see cref="Slice{TState}"/>.</summary>
/// <typeparam name="TState">The slice state.</typeparam>
[Obsolete("Ducky 2.0 removed ISlice<TState>: write `sealed class X : Slice<TState>` with `protected override TState Initial =>`.", error: true, DiagnosticId = "DUCKYM002", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISlice<TState>;

/// <summary>Removed in Ducky 2.0 (DUCKYM003): derive from <see cref="Effect{TAction}"/>.</summary>
/// <typeparam name="TAction">The handled action.</typeparam>
[Obsolete("Ducky 2.0 removed AsyncEffect<TAction>: derive from Effect<TAction> (constructor injection, Handle(action, EffectContext, CancellationToken), ctx.Dispatch, ctx.State).", error: true, DiagnosticId = "DUCKYM003", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface AsyncEffect<TAction>;

/// <summary>Removed in Ducky 2.0 (DUCKYM003): derive from <see cref="Effect{TAction}"/>.</summary>
[Obsolete("Ducky 2.0 removed IAsyncEffect: derive from Effect<TAction> (constructor injection, Handle(action, EffectContext, CancellationToken), ctx.Dispatch, ctx.State).", error: true, DiagnosticId = "DUCKYM003", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAsyncEffect;

/// <summary>Removed in Ducky 2.0 (DUCKYM004): derive from Ducky.Reactive's <c>ReactiveEffect</c>.</summary>
[Obsolete("Ducky 2.0 removed IReactiveEffect: derive from Ducky.Reactive's ReactiveEffect (R3 Observable<object>).", error: true, DiagnosticId = "DUCKYM004", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IReactiveEffect;

/// <summary>Removed in Ducky 2.0 (DUCKYM005): use <c>Selector.Define(...)</c> and <c>def.Create()</c>.</summary>
[Obsolete("Ducky 2.0 removed MemoizedSelector: use Selector.Define(...) and def.Create().", error: true, DiagnosticId = "DUCKYM005", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface MemoizedSelector;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
[Obsolete("Ducky 2.0 removed Fsa: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface Fsa;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
/// <typeparam name="TPayload">The payload.</typeparam>
[Obsolete("Ducky 2.0 removed Fsa<TPayload>: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface Fsa<TPayload>;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
/// <typeparam name="TPayload">The payload.</typeparam>
/// <typeparam name="TMeta">The metadata.</typeparam>
[Obsolete("Ducky 2.0 removed Fsa<TPayload, TMeta>: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface Fsa<TPayload, TMeta>;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
[Obsolete("Ducky 2.0 removed FsaError: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface FsaError;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
/// <typeparam name="TMeta">The metadata.</typeparam>
[Obsolete("Ducky 2.0 removed FsaError<TMeta>: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface FsaError<TMeta>;

/// <summary>Removed in Ducky 2.0 (DUCKYM006): use a plain record.</summary>
/// <typeparam name="TMeta">The metadata.</typeparam>
[Obsolete("Ducky 2.0 removed FsaMeta<TMeta>: use a plain record with no base type.", error: true, DiagnosticId = "DUCKYM006", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface FsaMeta<TMeta>;

/// <summary>Removed in Ducky 2.0 (DUCKYM007): give the state record an <c>EntityState&lt;TKey, TEntity&gt;</c> member.</summary>
/// <typeparam name="TKey">The entity key.</typeparam>
/// <typeparam name="TEntity">The entity.</typeparam>
/// <typeparam name="TState">The deriving state.</typeparam>
[Obsolete("Ducky 2.0 removed NormalizedState<TKey, TEntity, TState>: give the state record an EntityState<TKey, TEntity> member.", error: true, DiagnosticId = "DUCKYM007", UrlFormat = Tombstone.UrlFormat)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface NormalizedState<TKey, TEntity, TState>;
#pragma warning restore CA1715
