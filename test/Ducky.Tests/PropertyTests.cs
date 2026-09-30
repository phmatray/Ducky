using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Tests;

// Self-check of test/Shared/Property.cs (non-normative): the gated runs set DUCKY_PROPERTY_SEEDS (SPEC §17.1).
public sealed class PropertyTests
{
    [Fact]
    public void Check_GatedRun_ReplaysEachCommittedSeedOnce()
    {
        var cases = 0;

        Property.Check(Gen.Int, _ => Interlocked.Increment(ref cases));

        if (Environment.GetEnvironmentVariable("DUCKY_PROPERTY_SEEDS") is null)
        {
            cases.ShouldBeGreaterThan(1);
        }
        else
        {
            var seeds = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "property-seeds.txt"))
                .Count(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'));
            seeds.ShouldBeGreaterThan(0);
            cases.ShouldBe(seeds);
        }
    }
}
