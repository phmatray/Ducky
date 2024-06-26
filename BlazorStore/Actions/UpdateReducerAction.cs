namespace BlazorStore;

public class UpdateReducersAction(IEnumerable<string> features)
    : IAction
{
    public string Type => "UPDATE_REDUCERS";
    public IEnumerable<string> Features { get; } = features;
}