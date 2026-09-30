namespace Probe.Empty;

/// <summary>No executable line: the implicit constructor has no sequence point.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MarkerAttribute : Attribute;
