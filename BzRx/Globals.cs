using System.Collections.Concurrent;

namespace BzRx;

public static class Globals
{
    public static readonly ConcurrentDictionary<string, int> RegisteredActionTypes = new();

    public static void ResetRegisteredActionTypes()
    {
        RegisteredActionTypes.Clear();
    }
}