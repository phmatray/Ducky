using System.ComponentModel.DataAnnotations;

namespace Ducky.CodeGen.WebApp.Models;

public class AppStore
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Namespace { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<StateSlice> StateSlices { get; set; } = new();
    public List<GeneratedFile> GeneratedFiles { get; set; } = new();
}

public class StateSlice
{
    public int Id { get; set; }
    public int AppStoreId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public string StateDefinition { get; set; } = string.Empty; // JSON representation of the state
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public AppStore AppStore { get; set; } = null!;
    public List<ActionDefinition> Actions { get; set; } = new();
    public List<EffectDefinition> Effects { get; set; } = new();
}

public class ActionDefinition
{
    public int Id { get; set; }
    public int StateSliceId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public string PayloadType { get; set; } = string.Empty; // C# type definition
    
    public bool IsAsync { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public StateSlice StateSlice { get; set; } = null!;
}

public class EffectDefinition
{
    public int Id { get; set; }
    public int StateSliceId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public string ImplementationType { get; set; } = string.Empty; // "AsyncEffect" or "ReactiveEffect"
    
    [Required]
    public string TriggerActions { get; set; } = string.Empty; // JSON array of action names
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public StateSlice StateSlice { get; set; } = null!;
}

public class GeneratedFile
{
    public int Id { get; set; }
    public int AppStoreId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string FileType { get; set; } = string.Empty; // "Duck", "State", "Actions", "Effects", etc.
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public AppStore AppStore { get; set; } = null!;
}