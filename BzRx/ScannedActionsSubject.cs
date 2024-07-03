using Microsoft.Extensions.DependencyInjection;
using R3;

namespace BzRx;

public class ScannedActionsSubject
    : Subject<Action>, IDisposable
{
    public void Dispose()
    {
        base.OnCompleted();
    }
}

public static class ScannedActionsSubjectProvider
{
    public static void AddScannedActionsSubject(this IServiceCollection services)
    {
        services.AddSingleton<ScannedActionsSubject>();
    }
}