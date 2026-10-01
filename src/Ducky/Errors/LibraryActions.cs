namespace Ducky;

/// <summary>Dispatched once when the store becomes ready, before the buffered user actions.</summary>
public sealed record StoreInitialized;

/// <summary>Dispatched when a reducer, <c>MayDispatch</c> or <c>BeforeReduce</c> throws for an action.</summary>
/// <param name="ActionType">The action type name of the failed action.</param>
/// <param name="SliceKey">The key of the slice whose reducer threw, or <see langword="null"/> when no reducer threw.</param>
/// <param name="Exception">The exception thrown.</param>
public sealed record ReducerFailed(string ActionType, string? SliceKey, Exception Exception);

/// <summary>Dispatched when an effect throws (other than its own cancellation).</summary>
/// <param name="EffectType">The effect type name.</param>
/// <param name="ActionType">The action type name of the action that started the effect.</param>
/// <param name="Exception">The exception thrown.</param>
public sealed record EffectFailed(string EffectType, string ActionType, Exception Exception);
