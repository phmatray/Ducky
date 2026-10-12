using Ducky.Blazor;
using Ducky.Generated;
using PackageSmoke;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<SmokeMiddleware>();
builder.Services.AddDucky(d =>
{
#if SMOKE_SINGLETON
    d.Lifetime = ServiceLifetime.Singleton;
#endif
    d.AddDuckyGenerated_PackageSmoke();
});
var app = builder.Build();
app.UseMiddleware<SmokeMiddleware>();
await app.RunAsync();

[ActionType("smoke/server")]
internal sealed class SmokeMiddleware(IDispatcher dispatcher) : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Items[nameof(PersistenceFailed)] = new PersistenceFailed("smoke", nameof(SmokeMiddleware), "server");
        dispatcher.Increment();
        return next(context);
    }
}
