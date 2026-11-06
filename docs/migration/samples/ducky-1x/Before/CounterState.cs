using Ducky;

namespace Ducky1x.Before;

// Ducky 1.x: every state implements the IState marker.
public sealed record CounterState(int Value) : IState;
