using Microsoft.EntityFrameworkCore;
using Ducky.CodeGen.WebApp.Data;
using Ducky.CodeGen.WebApp.Models;
using System.Text.Json;

namespace Ducky.CodeGen.WebApp.Services;

public interface IAppStoreService
{
    Task<List<AppStore>> GetAllAppStoresAsync();
    Task<AppStore?> GetAppStoreByIdAsync(int id);
    Task<AppStore> CreateAppStoreAsync(string name, string? description, string namespaceName);
    Task<AppStore> UpdateAppStoreAsync(AppStore appStore);
    Task DeleteAppStoreAsync(int id);
    Task<StateSlice> AddStateSliceAsync(int appStoreId, string name, string description, object stateDefinition);
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

    public async Task<List<AppStore>> GetAllAppStoresAsync()
    {
        return await _context.AppStores
            .Include(a => a.StateSlices)
                .ThenInclude(s => s.Actions)
            .Include(a => a.StateSlices)
                .ThenInclude(s => s.Effects)
            .Include(a => a.GeneratedFiles)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();
    }

    public async Task<AppStore?> GetAppStoreByIdAsync(int id)
    {
        return await _context.AppStores
            .Include(a => a.StateSlices)
                .ThenInclude(s => s.Actions)
            .Include(a => a.StateSlices)
                .ThenInclude(s => s.Effects)
            .Include(a => a.GeneratedFiles)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AppStore> CreateAppStoreAsync(string name, string? description, string namespaceName)
    {
        var appStore = new AppStore
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
        var appStore = await _context.AppStores.FindAsync(id);
        if (appStore != null)
        {
            _context.AppStores.Remove(appStore);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<StateSlice> AddStateSliceAsync(int appStoreId, string name, string description, object stateDefinition)
    {
        var stateSlice = new StateSlice
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
        var appStore = await _context.AppStores.FindAsync(appStoreId);
        if (appStore != null)
        {
            appStore.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return stateSlice;
    }

    public async Task<ActionDefinition> AddActionAsync(int stateSliceId, string name, string description, string payloadType, bool isAsync = false)
    {
        var action = new ActionDefinition
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
        var effect = new EffectDefinition
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
        var appStore = await GetAppStoreByIdAsync(appStoreId);
        if (appStore == null)
        {
            throw new ArgumentException($"AppStore with ID {appStoreId} not found");
        }

        // Remove existing generated files
        var existingFiles = await _context.GeneratedFiles
            .Where(f => f.AppStoreId == appStoreId)
            .ToListAsync();
        _context.GeneratedFiles.RemoveRange(existingFiles);

        // Generate new files
        var generatedFiles = _codeGenerator.GenerateAppStore(appStore);

        // Save to database
        foreach (var file in generatedFiles)
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
        var slice = await _context.StateSlices
            .Include(s => s.AppStore)
            .FirstOrDefaultAsync(s => s.Id == stateSliceId);

        if (slice != null)
        {
            slice.UpdatedAt = DateTime.UtcNow;
            slice.AppStore.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}