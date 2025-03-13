using Ducky.Generators;

namespace Demo.BlazorWasm;

// This code will not compile until you build the project with the Source Generators

[DuckyAction]
public partial class SampleEntity
{
    public int Id { get; } = 42;
    public string? Name { get; } = "Sample";
}
