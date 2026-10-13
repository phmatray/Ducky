namespace System.Runtime.CompilerServices;

// netstandard2.0 lacks the marker type that init accessors (the pipeline's positional records) compile against.
internal static class IsExternalInit
{
}
