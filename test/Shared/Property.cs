using CsCheck;

namespace Ducky.TestSupport;

/// <summary>The one CsCheck entry point of the covered test projects (SPEC §17.1).</summary>
internal static class Property
{
    /// <summary>
    /// Runs <paramref name="assert"/> over <paramref name="gen"/>. With DUCKY_PROPERTY_SEEDS set (the gated runs), each
    /// seed of build/property-seeds.txt runs exactly once, on one thread; otherwise CsCheck samples randomly.
    /// </summary>
    public static void Check<T>(Gen<T> gen, Action<T> assert)
    {
#pragma warning disable RS0030 // justification: the one CsCheck entry point of the covered projects (§17.1)
        if (Environment.GetEnvironmentVariable("DUCKY_PROPERTY_SEEDS") is null)
        {
            gen.Sample(assert);
            return;
        }

        // An empty seed list would run zero cases and pass every property vacuously.
        var seeds = Seeds().ToList();
        if (seeds.Count == 0)
        {
            throw new InvalidOperationException("DUCKY_PROPERTY_SEEDS is set but build/property-seeds.txt has no seeds");
        }

        foreach (var seed in seeds)
        {
            gen.Sample(assert, seed: seed, iter: 1, threads: 1);
        }
#pragma warning restore RS0030
    }

    private static IEnumerable<string> Seeds() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "property-seeds.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));
}
