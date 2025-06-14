using System.Text.Json;
using Ducky.Generator.WebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Dialogs;

public partial class StateSliceDesignerDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public StateSlice? StateSlice { get; set; }

    [Parameter]
    public int AppStoreId { get; set; }

    private StateSlice? _stateSlice;
    private bool _isValid = true;
    private string _sliceName = string.Empty;
    private string _sliceDescription = string.Empty;
    private string _selectedTemplate = "custom";
    private List<StateProperty> _stateProperties = [];
    private List<ActionDefinition> _actions = [];

    public class StateProperty
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public string DefaultValue { get; set; } = string.Empty;
    }

    protected override void OnInitialized()
    {
        _stateSlice = StateSlice;

        if (_stateSlice is not null)
        {
            _sliceName = _stateSlice.Name;
            _sliceDescription = _stateSlice.Description ?? string.Empty;
            _actions = _stateSlice.Actions.ToList();

            // Parse state definition
            if (!string.IsNullOrEmpty(_stateSlice.StateDefinition))
            {
                try
                {
                    Dictionary<string, object>? stateDict = 
                        JsonSerializer.Deserialize<Dictionary<string, object>>(_stateSlice.StateDefinition);
                    
                    if (stateDict is not null)
                    {
                        _stateProperties = stateDict
                            .Select(kvp => new StateProperty
                            {
                                Name = kvp.Key,
                                Type = kvp.Value?.ToString() ?? "object",
                                DefaultValue = GetDefaultValueForType(kvp.Value?.ToString() ?? "object")
                            })
                            .ToList();
                    }
                }
                catch
                {
                    // If parsing fails, start with empty properties
                    _stateProperties = new List<StateProperty>();
                }
            }
        }
        else
        {
            // Default properties for new state slice
            _stateProperties = new List<StateProperty>
            {
                new StateProperty { Name = "IsLoading", Type = "bool", DefaultValue = "false" },
                new StateProperty { Name = "Error", Type = "string?", DefaultValue = "null" }
            };
        }
    }

    private void ApplyTemplate(string template)
    {
        _selectedTemplate = template;

        switch (template)
        {
            case "crud":
                {
                    ApplyCrudTemplate();
                    break;
                }
            case "async-data":
                {
                    ApplyAsyncDataTemplate();
                    break;
                }
            case "form":
                {
                    ApplyFormTemplate();
                    break;
                }
            case "list-with-filter":
                {
                    ApplyListWithFilterTemplate();
                    break;
                }
        }

        StateHasChanged();
    }

    private void ApplyCrudTemplate()
    {
        _stateProperties = new List<StateProperty>
        {
            new StateProperty { Name = "Items", Type = "NormalizedState<int, TEntity>", DefaultValue = "new()" },
            new StateProperty { Name = "SelectedItem", Type = "TEntity?", DefaultValue = "null" },
            new StateProperty { Name = "IsLoading", Type = "bool", DefaultValue = "false" },
            new StateProperty { Name = "IsSaving", Type = "bool", DefaultValue = "false" },
            new StateProperty { Name = "Error", Type = "string?", DefaultValue = "null" }
        };

        _actions = new List<ActionDefinition>
        {
            new ActionDefinition
                { Name = "LoadItems", PayloadType = "{}", IsAsync = true, Description = "Load all items" },
            new ActionDefinition
                { Name = "SelectItem", PayloadType = "{ Id: int }", IsAsync = false, Description = "Select an item" },
            new ActionDefinition
                { Name = "CreateItem", PayloadType = "TEntity", IsAsync = true, Description = "Create a new item" },
            new ActionDefinition
                { Name = "UpdateItem", PayloadType = "TEntity", IsAsync = true, Description = "Update existing item" },
            new ActionDefinition
                { Name = "DeleteItem", PayloadType = "{ Id: int }", IsAsync = true, Description = "Delete an item" },
            new ActionDefinition
            {
                Name = "SetLoading",
                PayloadType = "{ IsLoading: bool }",
                IsAsync = false,
                Description = "Set loading state"
            },
            new ActionDefinition
            {
                Name = "SetError", PayloadType = "{ Error: string? }", IsAsync = false, Description = "Set error state"
            }
        };
    }

    private void ApplyAsyncDataTemplate()
    {
        _stateProperties = new List<StateProperty>
        {
            new StateProperty { Name = "Data", Type = "TData?", DefaultValue = "null" },
            new StateProperty { Name = "IsLoading", Type = "bool", DefaultValue = "false" },
            new StateProperty { Name = "LastFetch", Type = "DateTime?", DefaultValue = "null" },
            new StateProperty { Name = "Error", Type = "string?", DefaultValue = "null" }
        };

        _actions = new List<ActionDefinition>
        {
            new ActionDefinition
            {
                Name = "FetchData",
                PayloadType = "{}",
                IsAsync = true,
                Description = "Fetch data from API"
            },
            new ActionDefinition
            {
                Name = "FetchDataSuccess",
                PayloadType = "{ Data: TData }",
                IsAsync = false,
                Description = "Data fetch succeeded"
            },
            new ActionDefinition
            {
                Name = "FetchDataFailure",
                PayloadType = "{ Error: string }",
                IsAsync = false,
                Description = "Data fetch failed"
            },
            new ActionDefinition
            {
                Name = "RefreshData",
                PayloadType = "{}",
                IsAsync = true,
                Description = "Refresh data"
            },
            new ActionDefinition
            {
                Name = "ClearData",
                PayloadType = "{}",
                IsAsync = false,
                Description = "Clear cached data"
            }
        };
    }

    private void ApplyFormTemplate()
    {
        _stateProperties = new List<StateProperty>
        {
            new StateProperty { Name = "FormData", Type = "TFormData", DefaultValue = "new()" },
            new StateProperty { Name = "Errors", Type = "Dictionary<string, string>", DefaultValue = "new()" },
            new StateProperty { Name = "IsValid", Type = "bool", DefaultValue = "true" },
            new StateProperty { Name = "IsSubmitting", Type = "bool", DefaultValue = "false" },
            new StateProperty { Name = "IsDirty", Type = "bool", DefaultValue = "false" }
        };

        _actions = new List<ActionDefinition>
        {
            new ActionDefinition
            {
                Name = "UpdateField",
                PayloadType = "{ FieldName: string, Value: object }",
                IsAsync = false,
                Description = "Update form field"
            },
            new ActionDefinition
            {
                Name = "SetFieldError",
                PayloadType = "{ FieldName: string, Error: string }",
                IsAsync = false,
                Description = "Set field validation error"
            },
            new ActionDefinition
            {
                Name = "ClearFieldError",
                PayloadType = "{ FieldName: string }",
                IsAsync = false,
                Description = "Clear field error"
            },
            new ActionDefinition
            {
                Name = "SubmitForm",
                PayloadType = "{}",
                IsAsync = true,
                Description = "Submit the form"
            },
            new ActionDefinition
            {
                Name = "ResetForm",
                PayloadType = "{}",
                IsAsync = false,
                Description = "Reset form to initial state"
            },
            new ActionDefinition
            {
                Name = "SetSubmitting",
                PayloadType = "{ IsSubmitting: bool }",
                IsAsync = false,
                Description = "Set form submission state"
            }
        };
    }

    private void ApplyListWithFilterTemplate()
    {
        _stateProperties = new List<StateProperty>
        {
            new StateProperty { Name = "Items", Type = "List<TItem>", DefaultValue = "new()" },
            new StateProperty { Name = "FilteredItems", Type = "List<TItem>", DefaultValue = "new()" },
            new StateProperty { Name = "SearchQuery", Type = "string", DefaultValue = "string.Empty" },
            new StateProperty { Name = "SortBy", Type = "string", DefaultValue = "string.Empty" },
            new StateProperty { Name = "SortDirection", Type = "SortDirection", DefaultValue = "SortDirection.Ascending" },
            new StateProperty { Name = "CurrentPage", Type = "int", DefaultValue = "1" },
            new StateProperty { Name = "PageSize", Type = "int", DefaultValue = "10" },
            new StateProperty { Name = "IsLoading", Type = "bool", DefaultValue = "false" }
        };

        _actions = new List<ActionDefinition>
        {
            new ActionDefinition
            {
                Name = "LoadItems",
                PayloadType = "{}",
                IsAsync = true,
                Description = "Load items"
            },
            new ActionDefinition
            {
                Name = "SetSearchQuery",
                PayloadType = "{ Query: string }",
                IsAsync = false,
                Description = "Set search query"
            },
            new ActionDefinition
            {
                Name = "SetSort",
                PayloadType = "{ SortBy: string, Direction: SortDirection }",
                IsAsync = false,
                Description = "Set sort criteria"
            },
            new ActionDefinition
            {
                Name = "SetPage",
                PayloadType = "{ Page: int }",
                IsAsync = false,
                Description = "Set current page"
            },
            new ActionDefinition
            {
                Name = "SetPageSize",
                PayloadType = "{ PageSize: int }",
                IsAsync = false,
                Description = "Set page size"
            },
            new ActionDefinition
            {
                Name = "ApplyFilters",
                PayloadType = "{}",
                IsAsync = false,
                Description = "Apply current filters"
            }
        };
    }

    private void AddStateProperty()
    {
        _stateProperties.Add(new StateProperty
        {
            Name = $"Property{_stateProperties.Count + 1}",
            Type = "string",
            DefaultValue = "string.Empty"
        });
    }

    private void RemoveStateProperty(int index)
    {
        if (index < 0 || index >= _stateProperties.Count)
        {
            return;
        }

        _stateProperties.RemoveAt(index);
    }

    private void AddAction()
    {
        _actions.Add(new ActionDefinition
        {
            Name = $"Action{_actions.Count + 1}",
            PayloadType = "{}",
            IsAsync = false,
            Description = "New action"
        });
    }

    private void RemoveAction(int index)
    {
        if (index < 0 || index >= _actions.Count)
        {
            return;
        }

        _actions.RemoveAt(index);
    }

    private string GetDefaultValueForType(string type)
    {
        return type.ToLower() switch
        {
            "bool" => "false",
            "int" => "0",
            "decimal" => "0m",
            "string" => "string.Empty",
            "string?" => "null",
            "datetime" => "DateTime.Now",
            _ when type.StartsWith("List<") => "new()",
            _ when type.StartsWith("NormalizedState<") => "new()",
            _ when type.EndsWith("?") => "null",
            _ => "new()"
        };
    }

    private string GenerateStatePreview()
    {
        if (string.IsNullOrWhiteSpace(_sliceName) || _stateProperties.Count == 0)
        {
            return "// Add properties to see preview";
        }

        List<string> properties = _stateProperties
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => $"    public {p.Type} {p.Name} {{ get; init; }} = {p.DefaultValue};")
            .ToList();

        return $"public record {_sliceName}State : IState\n{{\n{string.Join("\n", properties)}\n}}";
    }

    private string GenerateActionsPreview()
    {
        if (_actions.Count == 0)
        {
            return "// Add actions to see preview";
        }

        List<string> actionRecords = _actions
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => $"public record {a.Name}Action({a.PayloadType});")
            .ToList();

        return string.Join("\n\n", actionRecords);
    }

    private string GenerateReducersPreview()
    {
        if (string.IsNullOrWhiteSpace(_sliceName) || _actions.Count == 0)
        {
            return "// Add actions to see preview";
        }

        List<string> reducerMethods = _actions
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => $"        .On<{a.Name}Action>((state, action) => state with {{ /* TODO: Implement */ }})")
            .ToList();

        return
            $"public static class {_sliceName}Reducers\n{{\n    public static SliceReducers<{_sliceName}State> CreateReducers()\n    {{\n        return new SliceReducers<{_sliceName}State>(new {_sliceName}State())\n{string.Join("\n", reducerMethods)};\n    }}\n}}";
    }

    private void Cancel() => MudDialog.Cancel();

    private void Save()
    {
        if (!_isValid || string.IsNullOrWhiteSpace(_sliceName))
        {
            return;
        }

        Dictionary<string, object> stateDefinition = _stateProperties
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToDictionary(p => p.Name, p => (object)p.Type);

        var result = new
        {
            Name = _sliceName.Trim(),
            Description = string.IsNullOrWhiteSpace(_sliceDescription) ? null : _sliceDescription.Trim(),
            StateDefinition = stateDefinition,
            Actions = _actions.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList()
        };

        MudDialog.Close(DialogResult.Ok(result));
    }
}
