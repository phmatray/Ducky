using Microsoft.EntityFrameworkCore;
using Ducky.Generator.WebApp.Models;

namespace Ducky.Generator.WebApp.Data;

public class CodeGenDbContext : DbContext
{
    public CodeGenDbContext(DbContextOptions<CodeGenDbContext> options) : base(options)
    {
    }

    public DbSet<AppStore> AppStores { get; set; }
    public DbSet<StateSlice> StateSlices { get; set; }
    public DbSet<ActionDefinition> ActionDefinitions { get; set; }
    public DbSet<EffectDefinition> EffectDefinitions { get; set; }
    public DbSet<GeneratedFile> GeneratedFiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure AppStore
        modelBuilder.Entity<AppStore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Namespace).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique();
            
            // Configure relationships
            entity.HasMany(e => e.StateSlices)
                .WithOne(e => e.AppStore)
                .HasForeignKey(e => e.AppStoreId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.GeneratedFiles)
                .WithOne(e => e.AppStore)
                .HasForeignKey(e => e.AppStoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StateSlice
        modelBuilder.Entity<StateSlice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StateDefinition).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            
            // Configure relationships
            entity.HasMany(e => e.Actions)
                .WithOne(e => e.StateSlice)
                .HasForeignKey(e => e.StateSliceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.Effects)
                .WithOne(e => e.StateSlice)
                .HasForeignKey(e => e.StateSliceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ActionDefinition
        modelBuilder.Entity<ActionDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PayloadType).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Configure EffectDefinition
        modelBuilder.Entity<EffectDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ImplementationType).IsRequired();
            entity.Property(e => e.TriggerActions).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Configure GeneratedFile
        modelBuilder.Entity<GeneratedFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Content).IsRequired();
        });
    }
}
