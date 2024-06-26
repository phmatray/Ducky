namespace BlazorStore;

public static class Utils
{
    public static IDictionary<TKey, TValue> Omit<TKey, TValue>(
        IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        var copy = new Dictionary<TKey, TValue>(dictionary);
        copy.Remove(key);
        return copy;
    }
}