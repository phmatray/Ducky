// A hand-named *.g.cs that no test calls: build/coverage.settings.xml must drop it from the report (spikes.md, S-2).
namespace Probe;

/// <summary>Never called by the tests.</summary>
public static class Uncovered
{
    /// <summary>An uncovered line with two uncovered branches.</summary>
    public static int Sign(int value) => value > 0 ? 1 : -1;
}
