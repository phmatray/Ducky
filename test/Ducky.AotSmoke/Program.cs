using Ducky;

// SPEC §10: one PASS <name>/FAIL <name> line per named assertion, exit code 1 on any FAIL. Every name is a manifest name
// mapped to Ducky.AotSmoke (the AotSmoke target compares the PASS set with the active entries): area stories extend the
// existing assertions rather than print new names (PLAN P8).
var failed = false;

Check("AotSmoke", () => new ActionTypeAttribute("smoke/started").Name == "smoke/started");

return failed ? 1 : 0;

void Check(string name, Func<bool> assertion)
{
    bool passed;
    try
    {
        passed = assertion();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        passed = false;
    }
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
    failed |= !passed;
}
