using Ducky.Blazor;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<SmokeMiddleware>();
var app = builder.Build();
app.UseMiddleware<SmokeMiddleware>();
await app.RunAsync();

[ActionType("smoke/server")]
internal sealed class SmokeMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Items[nameof(PersistenceFailed)] = new PersistenceFailed("smoke", nameof(SmokeMiddleware), "server");
        return next(context);
    }
}
