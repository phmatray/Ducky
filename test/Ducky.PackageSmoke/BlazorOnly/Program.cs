using Ducky;
using Ducky.Generated;
using PackageSmoke;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDucky(d =>
{
#if SMOKE_SINGLETON
    d.Lifetime = ServiceLifetime.Singleton;
#endif
    d.AddDuckyGenerated_Ducky_PackageSmoke_BlazorOnly();
});
var app = builder.Build();
app.MapPost("/increment", (IDispatcher dispatcher) => dispatcher.Increment());
await app.RunAsync();
