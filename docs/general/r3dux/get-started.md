# Get Started

## Create Project & Install R3dux

```Bash
dotnet new blazor -o BlazorApp
cd BlazorApp
dotnet add package R3dux
dotnet add package R3dux.Blazor
```

## Prepare Startup

```C#
Program.cs

// Add RxStore
builder.Services.AddR3dux(builder.Configuration);
// ==== or ====
builder.Services.AddR3dux(options =>
{
    options.Assemblies = [typeof(CounterSlice).Assembly];
});
```
