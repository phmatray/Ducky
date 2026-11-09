#region duck
using System.Globalization;

namespace DuckShop;

// One duck the shop sells. A record: its properties are set once, when it is created.
public sealed record Duck(string Id, string Name, decimal Price)
{
    public string PriceText => Price.ToString("€0.00", CultureInfo.InvariantCulture);
}
#endregion
