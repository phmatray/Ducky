using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Ducky.Generator.WebApp.Data;
using Ducky.Generator.WebApp.Models;

namespace Ducky.Generator.WebApp.Services;

public interface IAppStoreService
{
    Task<List<AppStore>> GetAllAppStoresAsync();
    Task<AppStore?> GetAppStoreByIdAsync(int id);
    Task<AppStore> CreateAppStoreAsync(string name, string? description, string namespaceName);
    Task<AppStore> UpdateAppStoreAsync(AppStore appStore);
    Task DeleteAppStoreAsync(int id);
    Task<StateSlice> AddStateSliceAsync(int appStoreId, string name, string description, object stateDefinition);
    Task DeleteStateSliceAsync(int stateSliceId);
    Task<ActionDefinition> AddActionAsync(int stateSliceId, string name, string description, string payloadType, bool isAsync = false);
    Task<EffectDefinition> AddEffectAsync(int stateSliceId, string name, string description, string implementationType, List<string> triggerActions);
    Task<List<GeneratedFile>> GenerateFilesAsync(int appStoreId);
}

public class AppStoreService : IAppStoreService
{
    private readonly CodeGenDbContext _context;
    private readonly IAppStoreCodeGenerator _codeGenerator;

    public AppStoreService(CodeGenDbContext context, IAppStoreCodeGenerator codeGenerator)
    {
        _context = context;
        _codeGenerator = codeGenerator;
    }

    public Task<List<AppStore>> GetAllAppStoresAsync()
    {
        return _context.AppStores
            .Include(a => a.StateSlices)
            .ThenInclude(s => s.Actions)
            .Include(a => a.StateSlices)
            .ThenInclude(s => s.Effects)
            .Include(a => a.GeneratedFiles)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();
    }

    public Task<AppStore?> GetAppStoreByIdAsync(int id)
    {
        return _context.AppStores
            .Include(a => a.StateSlices)
            .ThenInclude(s => s.Actions)
            .Include(a => a.StateSlices)
            .ThenInclude(s => s.Effects)
            .Include(a => a.GeneratedFiles)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AppStore> CreateAppStoreAsync(string name, string? description, string namespaceName)
    {
        AppStore appStore = new()
        {
            Name = name,
            Description = description,
            Namespace = namespaceName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AppStores.Add(appStore);
        await _context.SaveChangesAsync();
        return appStore;
    }

    public async Task<AppStore> UpdateAppStoreAsync(AppStore appStore)
    {
        appStore.UpdatedAt = DateTime.UtcNow;
        _context.AppStores.Update(appStore);
        await _context.SaveChangesAsync();
        return appStore;
    }

    public async Task DeleteAppStoreAsync(int id)
    {
        AppStore? appStore = await _context.AppStores.FindAsync(id);
        if (appStore is null)
        {
            return;
        }

        _context.AppStores.Remove(appStore);
        await _context.SaveChangesAsync();
    }

    public async Task<StateSlice> AddStateSliceAsync(int appStoreId, string name, string description, object stateDefinition)
    {
        StateSlice stateSlice = new()
        {
            AppStoreId = appStoreId,
            Name = name,
            Description = description,
            StateDefinition = JsonSerializer.Serialize(stateDefinition),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.StateSlices.Add(stateSlice);
        await _context.SaveChangesAsync();

        // Update parent app store timestamp
        AppStore? appStore = await _context.AppStores.FindAsync(appStoreId);
        if (appStore is not null)
        {
            appStore.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return stateSlice;
    }

    public async Task DeleteStateSliceAsync(int stateSliceId)
    {
        StateSlice? stateSlice = await _context.StateSlices
            .Include(s => s.Actions)
            .Include(s => s.Effects)
            .FirstOrDefaultAsync(s => s.Id == stateSliceId);

        if (stateSlice is null)
        {
            return;
        }

        int appStoreId = stateSlice.AppStoreId;

        // Remove related actions and effects (EF Core should handle this with cascade delete)
        _context.StateSlices.Remove(stateSlice);
        await _context.SaveChangesAsync();

        // Update parent app store timestamp
        AppStore? appStore = await _context.AppStores.FindAsync(appStoreId);
        if (appStore is null)
        {
            return;
        }

        appStore.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<ActionDefinition> AddActionAsync(int stateSliceId, string name, string description, string payloadType, bool isAsync = false)
    {
        ActionDefinition action = new()
        {
            StateSliceId = stateSliceId,
            Name = name,
            Description = description,
            PayloadType = payloadType,
            IsAsync = isAsync,
            CreatedAt = DateTime.UtcNow
        };

        _context.ActionDefinitions.Add(action);
        await _context.SaveChangesAsync();

        // Update parent slice and app store timestamps
        await UpdateParentTimestampsAsync(stateSliceId);

        return action;
    }

    public async Task<EffectDefinition> AddEffectAsync(int stateSliceId, string name, string description, string implementationType, List<string> triggerActions)
    {
        EffectDefinition effect = new()
        {
            StateSliceId = stateSliceId,
            Name = name,
            Description = description,
            ImplementationType = implementationType,
            TriggerActions = JsonSerializer.Serialize(triggerActions),
            CreatedAt = DateTime.UtcNow
        };

        _context.EffectDefinitions.Add(effect);
        await _context.SaveChangesAsync();

        // Update parent slice and app store timestamps
        await UpdateParentTimestampsAsync(stateSliceId);

        return effect;
    }

    public async Task<List<GeneratedFile>> GenerateFilesAsync(int appStoreId)
    {
        AppStore? appStore = await GetAppStoreByIdAsync(appStoreId);
        if (appStore is null)
        {
            throw new ArgumentException($"AppStore with ID {appStoreId} not found");
        }

        // Remove existing generated files
        List<GeneratedFile> existingFiles = await _context.GeneratedFiles
            .Where(f => f.AppStoreId == appStoreId)
            .ToListAsync();
        _context.GeneratedFiles.RemoveRange(existingFiles);

        // Generate new files
        List<GeneratedFile> generatedFiles = _codeGenerator.GenerateAppStore(appStore);

        // Save to database
        foreach (GeneratedFile file in generatedFiles)
        {
            file.AppStoreId = appStoreId;
            file.GeneratedAt = DateTime.UtcNow;
        }

        _context.GeneratedFiles.AddRange(generatedFiles);
        await _context.SaveChangesAsync();

        return generatedFiles;
    }

    private async Task UpdateParentTimestampsAsync(int stateSliceId)
    {
        StateSlice? slice = await _context.StateSlices
            .Include(s => s.AppStore)
            .FirstOrDefaultAsync(s => s.Id == stateSliceId);

        if (slice is null)
        {
            return;
        }

        slice.UpdatedAt = DateTime.UtcNow;
        slice.AppStore.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
